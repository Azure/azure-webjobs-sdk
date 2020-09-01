// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
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
            var builder = new HostBuilder()
                .UseEnvironment("Development")
                .ConfigureWebJobs(b =>
                {
                    b.AddAzureStorageCoreServices()
                    .AddAzureStorage()
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

                    // Add a default stragegy. Any strategies applied via attribute at the
                    // class/method level will override this
                    var retryStrategy = new FixedDelayRetryAttribute(3, "00:00:10");
                    services.AddSingleton<IRetryStrategy>(retryStrategy);
                })
                .UseConsoleLifetime();

            var host = builder.Build();

            var jobHost = host.Services.GetRequiredService<IJobHost>();
            var arguments = new Dictionary<string, object>
            {
                { "blob", "test/test" }
            };
            await jobHost.CallAsync("BlobTrigger", arguments);

            using (host)
            {
                await host.RunAsync();
            }
        }
    }
}
