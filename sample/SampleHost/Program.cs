// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SampleHost
{
    class Program
    {
        private static Timer _scaleStatusTimer;

        public static async Task Main(string[] args)
        {
            var builder = new HostBuilder()
                .UseEnvironment("Development")
                .ConfigureWebJobs(b =>
                {
                    b.AddAzureStorageCoreServices()
                    .AddAzureStorageQueues();
                })
                .ConfigureAppConfiguration(b =>
                {
                    // Adding command line as a configuration source
                    b.AddCommandLine(args);
                })
                .ConfigureLogging((context, b) =>
                {
                    b.SetMinimumLevel(LogLevel.Trace);
                    b.AddConsole();

                    b.AddFilter("Azure.Core", LogLevel.Error);

                    // If this key exists in any config, use it to enable App Insights
                    string appInsightsKey = context.Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];
                    if (!string.IsNullOrEmpty(appInsightsKey))
                    {
                        b.AddApplicationInsightsWebJobs(o => o.InstrumentationKey = appInsightsKey);
                    }
                })
                .ConfigureServices(services =>
                {
                    // add some sample services to demonstrate job class DI
                    services.AddSingleton<ISampleServiceA, SampleServiceA>();
                    services.AddSingleton<ISampleServiceB, SampleServiceB>();

                    services.AddDynamicListeners();
                })
                .UseConsoleLifetime();

            var host = builder.Build();
            using (host)
            {
                // verify the customer status provider is working
                var functionActivityStatusProvider = host.Services.GetService<IFunctionActivityStatusProvider>();
                var status = functionActivityStatusProvider.GetStatus();

                var scaleStatusProvider = host.Services.GetService<IScaleStatusProvider>();

                _scaleStatusTimer = new Timer(OnScaleStatusTimer, scaleStatusProvider, 5000, 5000);

                await host.RunAsync();
            }
        }

        private static async void OnScaleStatusTimer(object state)
        {
            IScaleStatusProvider scaleStatusProvider = (IScaleStatusProvider)state;
            var context = new ScaleStatusContext
            {
                WorkerCount = 1
            };
            var result = await scaleStatusProvider.GetScaleStatusAsync(context);
        }
    }
}
