// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SampleHost
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args)
                .UseEnvironment(EnvironmentName.Development)
                .ConfigureWebJobs(b =>
                {
                    b.AddAzureStorageCoreServices()
                    .AddAzureStorageBlobs()
                    .AddAzureStorageQueues()
                    .AddServiceBus()
                    .AddEventHubs();
                })
                .ConfigureAppConfiguration(b =>
                {
                    // Adding command line as a configuration source
                    b.AddCommandLine(args);
                })
                .ConfigureLogging((context, b) =>
                {
                    b.SetMinimumLevel(LogLevel.Debug);
                    b.AddConsole();
                })
                .ConfigureServices(services =>
                {
                    // add some sample services to demonstrate job class DI
                    services.AddSingleton<ISampleServiceA, SampleServiceA>();
                    services.AddSingleton<ISampleServiceB, SampleServiceB>();
                    services.AddApplicationInsightsTelemetryWorkerService();
                })
                .UseConsoleLifetime();

            using var host = builder.Build();
            await host.RunAsync();
        }
    }
}
