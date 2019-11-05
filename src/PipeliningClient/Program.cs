using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace PipeliningClient
{
    class Program
    {
        private static int _counter;
        public static void IncrementCounter() => Interlocked.Increment(ref _counter);

        private static int _errors;
        public static void IncrementError() => Interlocked.Increment(ref _errors);

        private static int _socketErrors;
        public static void IncrementSocketError() => Interlocked.Increment(ref _socketErrors);

        private static int _running;
        public static bool IsRunning => _running == 1;

        public static string ServerUrl { get; set; }
        public static int PipelineDepth { get; set; }
        public static int WarmupTimeSeconds { get; set; }
        public static int ExecutionTimeSeconds { get; set; }
        public static int Connections { get; set; }
        public static List<string> Headers { get; set; }

        static async Task Main(string[] args)
        {
            var app = new CommandLineApplication();

            app.HelpOption("-h|--help");
            var optionUrl = app.Option("-u|--url <URL>", "The server url to request", CommandOptionType.SingleValue);
            var optionConnections = app.Option<int>("-c|--connections <N>", "Total number of HTTP connections to keep open", CommandOptionType.SingleValue);
            var optionWarmup = app.Option<int>("-w|--warmup <N>", "Duration of the warmup in seconds", CommandOptionType.SingleValue);
            var optionDuration = app.Option<int>("-d|--duration <N>", "Duration of the test in seconds", CommandOptionType.SingleValue);
            var optionHeaders = app.Option("-H|--header <HEADER>", "HTTP header to add to request, e.g. \"User-Agent: edge\"", CommandOptionType.MultipleValue);
            var optionPipeline = app.Option<int>("-p|--pipeline <N>", "The pipelining depth", CommandOptionType.SingleValue);

            app.OnExecuteAsync(cancellationToken =>
            {
                PipelineDepth = optionPipeline.HasValue()
                    ? int.Parse(optionPipeline.Value())
                    : 1;

                ServerUrl = optionUrl.Value();

                WarmupTimeSeconds = optionWarmup.HasValue()
                    ? int.Parse(optionWarmup.Value())
                    : 0;

                ExecutionTimeSeconds = int.Parse(optionDuration.Value());

                Connections = int.Parse(optionConnections.Value());

                Headers = new List<string>(optionHeaders.Values);

                return RunAsync();
            });

            await app.ExecuteAsync(args);            
        }

        public static async Task RunAsync()
        {
            Console.WriteLine($"Running {ExecutionTimeSeconds}s test @ {ServerUrl}");

            DateTime startTime = default, stopTime = default;

            var totalRequests = 0;
            var results = new List<double>();

            IEnumerable<Task> CreateTasks()
            {
                // Statistics thread
                yield return Task.Run(
                    async () =>
                    {
                        if (WarmupTimeSeconds > 0)
                        {
                            Console.WriteLine($"Warming up for {WarmupTimeSeconds}s...");
                            await Task.Delay(TimeSpan.FromSeconds(WarmupTimeSeconds));
                        }

                        Console.WriteLine($"Running for {ExecutionTimeSeconds}s...");

                        Interlocked.Exchange(ref _counter, 0);
                        Interlocked.Exchange(ref _errors, 0);
                        Interlocked.Exchange(ref _socketErrors, 0);

                        startTime = DateTime.UtcNow;
                        var lastDisplay = startTime;

                        while (IsRunning)
                        {
                            await Task.Delay(200);

                            var now = DateTime.UtcNow;
                            var tps = (int)(_counter / (now - lastDisplay).TotalSeconds);
                            var remaining = (int)(ExecutionTimeSeconds - (now - startTime).TotalSeconds);

                            results.Add(tps);

                            //Console.Write($"{tps} tps, {remaining}s                     ");
                            //Console.SetCursorPosition(0, Console.CursorTop);

                            lastDisplay = now;
                            totalRequests += Interlocked.Exchange(ref _counter, 0);
                        }
                    });

                // Shutdown everything
                yield return Task.Run(
                   async () =>
                   {
                       await Task.Delay(TimeSpan.FromSeconds(WarmupTimeSeconds + ExecutionTimeSeconds));

                       Interlocked.Exchange(ref _running, 0);

                       stopTime = DateTime.UtcNow;
                   });

                foreach (var task in Enumerable.Range(0, Connections)
                    .Select(_ => Task.Run(DoWorkAsync)))
                {
                    yield return task;
                }
            }

            Interlocked.Exchange(ref _running, 1);

            await Task.WhenAll(CreateTasks());

            var totalTps = (int)(totalRequests / (stopTime - startTime).TotalSeconds);

            results.Sort();
            results.RemoveAt(0);
            results.RemoveAt(results.Count - 1);

            double CalculateStdDev(ICollection<double> values)
            {
                var avg = values.Average();
                var sum = values.Sum(d => Math.Pow(d - avg, 2));

                return Math.Sqrt(sum / values.Count);
            }

            var stdDev = CalculateStdDev(results);

            Console.SetCursorPosition(0, Console.CursorTop);
            Console.WriteLine($"Average RPS:     {totalTps:N0}");
            Console.WriteLine($"Max RPS:         {results.Max():N0}");
            Console.WriteLine($"2xx:             {totalRequests:N0}");
            Console.WriteLine($"Bad Responses:   {_errors:N0}");
            Console.WriteLine($"Socket Errors:   {_socketErrors:N0}");
            Console.WriteLine($"StdDev:          {stdDev:N0}");
        }

        public static async Task DoWorkAsync()
        {
            while (IsRunning)
            {
                // Creating a new connection every time it is necessary
                using (var connection = new HttpConnection(ServerUrl, PipelineDepth, Headers))
                {
                    await connection.ConnectAsync();

                    try
                    {
                        var sw = new Stopwatch();

                        while (IsRunning)
                        {
                            sw.Start();

                            var responses = await connection.SendRequestsAsync();

                            sw.Stop();
                            // Add the latency divided by the pipeline depth

                            var doBreak = false;
                            
                            for (var k = 0; k < responses.Length; k++ )
                            {
                                var response = responses[k];

                                if (response.State == HttpResponseState.Completed)
                                {
                                    if (response.StatusCode >= 200 && response.StatusCode < 300)
                                    {
                                        IncrementCounter();
                                    }
                                    else
                                    {
                                        IncrementError();
                                    }
                                }
                                else
                                {
                                    IncrementSocketError();
                                    doBreak = true;
                                }
                            }

                            if (doBreak)
                            {
                                break;
                            }
                        }
                    }
                    catch
                    {
                        IncrementSocketError();
                    }
                }
            }
        }
    }
}
