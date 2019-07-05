using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ignitor;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IngitorClient
{
    class Program
    {
        const string BaseUri = "http://localhost:8000/";
        const int Count = 50000;

        static Task Main(string[] args)
        {
            var cts = new CancellationTokenSource(30 * 1000);
            return Navigator(cts.Token);
        }

        private static async Task Navigator(CancellationToken cancellationToken)
        {
            var links = new[] { "home", "fetchdata", "counter", "ticker" };
            var tasks = new Task[Count];
            
            for (var i = 0; i < Count; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    var connection = CreateHubConnection();
                    await connection.StartAsync();

                    var client = new BlazorClient(connection, BaseUri);

                    var link = 0;
                    await client.RunAsync(cancellationToken);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await client.NavigateTo(links[link], cancellationToken);
                        await client.WaitUntil(hive => hive.TryFindElementById(links[link] + "_displayed", out _));
                        link = (link + 1) % links.Length;
                    }
                });
            }

            await Task.WhenAll(tasks);
        }

        private static Task Rogue(CancellationToken cancellationToken)
        {
            var links = new[] { "home", "fetchdata", "counter", "ticker" };

            var slim = new SemaphoreSlim(4);
            var tasks = new List<Task>();
            for (var i = 0; i < 100; i++)
            {
                Console.WriteLine("Connecting...");
                tasks.Add(Task.Run(async () =>
                {
                    var link = 0;

                    await slim.WaitAsync();
                    var hubConnection = CreateHubConnection();

                    await hubConnection.StartAsync(cancellationToken);

                    var blazorClient = new BlazorClient(hubConnection, BaseUri);

                    await blazorClient.ConnectAsync(cancellationToken);
                    slim.Release();

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await blazorClient.NavigateTo(links[link], cancellationToken);
                        link = (link + 1) % links.Length;
                    }
                    Console.WriteLine("Connected...");


                    await blazorClient.DisposeAsync();
                }));
            }

            return Task.WhenAll(tasks);
        }


        private static Task NavigateTo(HubConnection hubConnection, string href, CancellationToken cancellationToken)
        {
            var assemblyName = "Microsoft.AspNetCore.Components.Server";
            var methodIdentifier = "NotifyLocationChanged";

            var argsObject = new object[] { $"{BaseUri}/{href}", true };
            var locationChangedArgs = JsonSerializer.Serialize(argsObject, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return hubConnection.SendAsync("BeginInvokeDotNetFromJS", "0", assemblyName, methodIdentifier, 0, locationChangedArgs, cancellationToken);
        }

        private static HubConnection CreateHubConnection()
        {
            var builder = new HubConnectionBuilder();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHubProtocol, IgnitorMessagePackHubProtocol>());
            builder.WithUrl(new Uri($"{BaseUri}_blazor/"));

            var connection = builder.Build();
            return connection;
        }
    }
}
