using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PipeliningClient
{
    class Program
    {
        private const int DefaultThreadCount = 256;
        private const int DefaultExecutionTimeSeconds = 15;
        private const int WarmupTimeSeconds = 10;

        private static int _counter;
        public static void IncrementCounter() => Interlocked.Increment(ref _counter);

        private static int _errors;
        public static void IncrementError() => Interlocked.Increment(ref _errors);

        private static int _socketErrors;
        public static void IncrementSocketError() => Interlocked.Increment(ref _socketErrors);

        private static int _running;
        public static bool IsRunning => _running == 1;

        public static string ServerUrl { get; set; }
        public static int PipelineDepth { get; set; } = 8;

        static async Task Main(string[] args)
        {
            Console.WriteLine("HttpClient Client");
            Console.WriteLine("args: " + String.Join(' ', args));
            Console.WriteLine("SERVER_URL:" + Environment.GetEnvironmentVariable("SERVER_URL"));

            ServerUrl = Environment.GetEnvironmentVariable("SERVER_URL");

            DateTime startTime = default, stopTime = default;

            var threadCount = DefaultThreadCount;
            var time = DefaultExecutionTimeSeconds;

            var totalRequests = 0;
            var results = new List<double>();

            IEnumerable<Task> CreateTasks()
            {
                // Statistics thread
                yield return Task.Run(
                    async () =>
                    {
                        Console.WriteLine($"Warming up for {WarmupTimeSeconds}s...");

                        await Task.Delay(TimeSpan.FromSeconds(WarmupTimeSeconds));

                        Console.WriteLine($"Running for {time}s...");

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
                            var remaining = (int)(time - (now - startTime).TotalSeconds);

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
                       await Task.Delay(TimeSpan.FromSeconds(WarmupTimeSeconds + time));

                       Interlocked.Exchange(ref _running, 0);

                       stopTime = DateTime.UtcNow;
                   });

                foreach (var task in Enumerable.Range(0, threadCount)
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
            Console.WriteLine($"{threadCount} Threads");
            Console.WriteLine($"Average RPS: {totalTps:N0}");
            Console.WriteLine($"Max RPS: {results.Max():N0}");
            Console.WriteLine($"20x: {totalRequests:N0}");
            Console.WriteLine($"Bad Responses: {_errors:N0}");
            Console.WriteLine($"Socket Errors: {_socketErrors:N0}");
            Console.WriteLine($"StdDev: {stdDev:N0}");
        }

        public static async Task DoWorkAsync()
        {
            var responses = new HttpResponse[PipelineDepth];

            while (IsRunning)
            {
                // Creating a new connection every time it is necessary
                using (var connection = new HttpConnection(ServerUrl, PipelineDepth))
                {
                    await connection.ConnectAsync();

                    try
                    {
                        var sw = new Stopwatch();

                        while (IsRunning)
                        {
                            sw.Start();

                            var i = 0;
                            await foreach (var response in connection.SendRequestsAsync())
                            {
                                responses[i++] = response;
                            }

                            sw.Stop();
                            // Add the latency divided by the pipeline depth

                            var doBreak = false;
                            foreach (var response in responses)
                            {
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
