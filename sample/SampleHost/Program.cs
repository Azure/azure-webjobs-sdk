// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace SampleHost
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = new HostBuilder()
                .UseEnvironment("Development")
                .ConfigureWebJobsHost()
                .AddWebJobsLogging() // Enables WebJobs v1 classic logging 
                .AddAzureStorageCoreServices()
                .AddAzureStorage()
                .AddApplicationInsights()
                .ConfigureAppConfiguration(config =>
                {
                    // Adding command line as a configuration source
                    config.AddCommandLine(args);
                })
                .ConfigureLogging(b =>
                {
                    b.SetMinimumLevel(LogLevel.Debug);
                    b.AddConsole();
                })
                .UseConsoleLifetime();

            var jobHost = builder.Build();

            using (jobHost)
            {
                await jobHost.RunAsync();
            }
        }
    }
}
