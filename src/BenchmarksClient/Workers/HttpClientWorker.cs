// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Benchmarks.ClientJob;
using Newtonsoft.Json;

namespace BenchmarksClient.Workers
{
    public class HttpClientWorker : IWorker
    {
        private HttpClient _httpClient;
        private HttpClientHandler _httpClientHandler;

        private static TimeSpan FirstRequestTimeout = TimeSpan.FromSeconds(5);
        private static TimeSpan LatencyTimeout = TimeSpan.FromSeconds(2);

        private ClientJob _job;
        private List<Task> _tasks = new List<Task>();
        private int[] _requestBuckets = null;
        private CancellationTokenSource _cts;

        public string JobLogText { get; set; }

        public HttpClientWorker()
        {
            // Configuring the http client to trust the self-signed certificate
            _httpClientHandler = new HttpClientHandler();
            _httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            _httpClient = new HttpClient(_httpClientHandler);
        }

        private void InitializeJob()
        {
            _job.ClientProperties.TryGetValue("ScriptName", out var scriptName);

            if (_job.ClientProperties.TryGetValue("PipelineDepth", out var pipelineDepth))
            {
                Debug.Assert(int.Parse(pipelineDepth) <= 0 || scriptName != null, "A script name must be present when the pipeline depth is larger than 0.");
            }

            var jobLogText =
                        $"[ID:{_job.Id} Connections:{_job.Connections} Threads:{_job.Threads} Duration:{_job.Duration} Method:{_job.Method} ServerUrl:{_job.ServerBenchmarkUri}";

            if (!string.IsNullOrEmpty(scriptName))
            {
                jobLogText += $" Script:{scriptName}";
            }

            if (pipelineDepth != null && int.Parse(pipelineDepth) > 0)
            {
                jobLogText += $" Pipeline:{pipelineDepth}";
            }

            if (_job.Headers != null)
            {
                jobLogText += $" Headers:{JsonConvert.SerializeObject(_job.Headers)}";
            }

            jobLogText += "]";

            JobLogText = jobLogText;

            _httpClientHandler.MaxConnectionsPerServer = _job.Connections;
            _requestBuckets = new int[_job.Connections];
        }

        public async Task StartJobAsync(ClientJob job)
        {
            _job = job;
            InitializeJob();

            await MeasureFirstRequestLatencyAsync(_job);

            _job.State = ClientState.Running;
            _job.LastDriverCommunicationUtc = DateTime.UtcNow;

            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(job.Duration));
            for (var i = 0; i < _job.Connections; i++)
            {
                var index = i;
                _tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        while (!_cts.IsCancellationRequested)
                        {
                            await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, job.ServerBenchmarkUri), _cts.Token);
                            _requestBuckets[index]++;
                        }
                    }
                    catch (TaskCanceledException)
                    {}
                }));
            }

            await Task.WhenAll(_tasks);

            await StopJobAsync();
        }

        public async Task StopJobAsync()
        {
            _cts?.Cancel();

            var totalRequests = 0;
            foreach (var requests in _requestBuckets)
            {
                totalRequests += requests;
            }
            _job.Requests = totalRequests;
            _job.RequestsPerSecond = _job.Requests / (double)_job.Duration;

            await Task.WhenAll(_tasks);

            _httpClient.Dispose();

            _job.State = ClientState.Completed;
        }

        public void Dispose()
        {
            _cts?.Cancel();
        }

        private static HttpRequestMessage CreateHttpMessage(ClientJob job)
        {
            var requestMessage = new HttpRequestMessage(new HttpMethod(job.Method), job.ServerBenchmarkUri);

            foreach (var header in job.Headers)
            {
                requestMessage.Headers.Add(header.Key, header.Value);
            }

            return requestMessage;
        }

        private async Task MeasureFirstRequestLatencyAsync(ClientJob job)
        {
            if (job.SkipStartupLatencies)
            {
                return;
            }

            Log($"Measuring first request latency on {job.ServerBenchmarkUri}");

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            using (var message = CreateHttpMessage(job))
            {
                var cts = new CancellationTokenSource();
                var token = cts.Token;
                cts.CancelAfter(FirstRequestTimeout);
                token.ThrowIfCancellationRequested();

                try
                {
                    using (var response = await _httpClient.SendAsync(message, token))
                    {
                        job.LatencyFirstRequest = stopwatch.Elapsed;
                    }
                }
                catch (OperationCanceledException)
                {
                    Log("A timeout occured while measuring the first request: " + FirstRequestTimeout.ToString());
                }
                finally
                {
                    cts.Dispose();
                }
            }

            Log($"{job.LatencyFirstRequest.TotalMilliseconds} ms");

            Log("Measuring subsequent requests latency");

            for (var i = 0; i < 10; i++)
            {
                stopwatch.Restart();

                using (var message = CreateHttpMessage(job))
                {
                    var cts = new CancellationTokenSource();
                    var token = cts.Token;
                    cts.CancelAfter(LatencyTimeout);
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        using (var response = await _httpClient.SendAsync(message))
                        {
                            // We keep the last measure to simulate a warmup phase.
                            job.LatencyNoLoad = stopwatch.Elapsed;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Log("A timeout occured while measuring the latency, skipping ...");
                        break;
                    }
                    finally
                    {
                        cts.Dispose();
                    }
                }
            }

            Log($"{job.LatencyNoLoad.TotalMilliseconds} ms");
        }

        private static void Log(string message)
        {
            var time = DateTime.Now.ToString("hh:mm:ss.fff");
            Console.WriteLine($"[{time}] {message}");
        }

        public Task DisposeAsync()
        {
            return StopJobAsync();
        }
    }
}