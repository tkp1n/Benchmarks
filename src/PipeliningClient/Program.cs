using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PipeliningClient
{
    class Program
    {
        static HttpClientHandler _httpClientHandler = new HttpClientHandler();
        static HttpClient _httpClient;

        static Program()
        {
            _httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            _httpClientHandler.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;
            _httpClient = new HttpClient(_httpClientHandler);
        }

        private const int DefaultThreadCount = 32;
        private const int DefaultExecutionTimeSeconds = 10;
        private const int WarmupTimeSeconds = 3;

        private static int _counter;
        public static void IncrementCounter() => Interlocked.Increment(ref _counter);

        private static int _error;
        public static void IncrementError() => Interlocked.Increment(ref _error);

        private static int _running;

        public static bool IsRunning => _running == 1;

        static async Task Main(string[] args)
        {
            Console.WriteLine("HttpClient Client");
            Console.WriteLine("args: " + String.Join(' ', args));
            Console.WriteLine("SERVER_URL:" + Environment.GetEnvironmentVariable("SERVER_URL"));

            DateTime startTime = default, stopTime = default;

            var threadCount = DefaultThreadCount;
            var time = DefaultExecutionTimeSeconds;

            var totalTransactions = 0;
            var results = new List<double>();

            IEnumerable<Task> CreateTasks()
            {
                yield return Task.Run(
                    async () =>
                    {
                        Console.Write($"Warming up for {WarmupTimeSeconds}s...");

                        await Task.Delay(TimeSpan.FromSeconds(WarmupTimeSeconds));

                        Interlocked.Exchange(ref _counter, 0);
                        Interlocked.Exchange(ref _error, 0);

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
                            totalTransactions += Interlocked.Exchange(ref _counter, 0);
                        }
                    });

                yield return Task.Run(
                   async () =>
                   {
                       await Task.Delay(TimeSpan.FromSeconds(WarmupTimeSeconds + time));

                       Interlocked.Exchange(ref _running, 0);

                       stopTime = DateTime.UtcNow;
                   });

                foreach (var task in Enumerable.Range(0, threadCount)
                    .Select(_ => Task.Factory.StartNew(DoWorkAsync, TaskCreationOptions.LongRunning).Unwrap()))
                {
                    yield return task;
                }
            }

            Interlocked.Exchange(ref _running, 1);

            await Task.WhenAll(CreateTasks());

            var totalTps = (int)(totalTransactions / (stopTime - startTime).TotalSeconds);

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
            Console.WriteLine($"{threadCount:D2} Threads, tps: {totalTps:F2}, Errors: {_error:D2}, stddev(w/o best+worst): {stdDev:F2}");
        }

        public static async Task DoWorkAsync()
        {
            var serverUrl = new Uri(Environment.GetEnvironmentVariable("SERVER_URL"));

            // TODO: parse server url
            string hostName = "10.0.0.102";
            int hostPort = 5000;

            var request = $"GET {serverUrl} HTTP/1.1\r\n" +
                $"Host: {hostName}:{hostPort}\r\n" +
                "Content-Length: 0\r\n" +
                "\r\n";

            var requestBytes = Encoding.UTF8.GetBytes(request).AsMemory();

            // http://10.0.0.102:5000/plaintext



            IPAddress host = IPAddress.Parse(hostName);
            IPEndPoint hostep = new IPEndPoint(host, hostPort);
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            await sock.ConnectAsync(hostep);

            var buffer = new byte[4096];
            var result = new char[buffer.Length * 2];
            int bytesReceived;

            while (Program.IsRunning)
            {
                var response = await sock.SendAsync(requestBytes, SocketFlags.None);

                var first = true;

                do
                {
                    bytesReceived = await sock.ReceiveAsync(buffer, SocketFlags.None);

                    if (first)
                    {
                        var expectedResponse = "HTTP/1.1 200 OK";
                        for (var i = 0; i < expectedResponse.Length; i++)
                        {
                            if (buffer[i] != expectedResponse[i])
                            {
                                throw new Exception("Bad Response");
                            }
                        }
                    }

                    //Encoding.UTF8.GetDecoder().Convert(buffer, 0, bytesReceived, result, 0, result.Length, true, out var bytesUsed, out var charsUsed, out var completed);
                    //Console.WriteLine(result);

                } while (bytesReceived == buffer.Length);
                Program.IncrementCounter();
            }

            sock.Close();
        }
    }
}
