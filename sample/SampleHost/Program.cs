// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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
                .ConfigureWebJobsHost(o =>
                {
                    // TEMP - remove once https://github.com/Azure/azure-webjobs-sdk/issues/1802 is fixed
                    o.HostId = "ecad61-62cf-47f4-93b4-6efcded6";
                })
                .AddWebJobsLogging() // Enables WebJobs v1 classic logging 
                .AddAzureStorageCoreServices()
                .AddAzureStorage()
                .AddServiceBus()
                .AddApplicationInsights()
                .ConfigureAppConfiguration(b =>
                {
                    // Adding command line as a configuration source
                    b.AddCommandLine(args);
                })
                .ConfigureLogging(b =>
                {
                    b.SetMinimumLevel(LogLevel.Debug);
                    b.AddConsole();
                })
                .UseConsoleLifetime();

            var host = builder.Build();
            using (host)
            {
                await host.RunAsync();
            }
        }
    }
}
