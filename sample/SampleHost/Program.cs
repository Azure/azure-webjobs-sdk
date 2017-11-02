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
                    // Example setting options properties:
                    // o.HostId = "testhostid";
                })
                .ConfigureAppConfiguration(config =>
                {
                    // Adding command line as a configuration source
                    config.AddCommandLine(args);
                    config.AddInMemoryCollection(new Dictionary<string, string>()
                    {
                        // Configuration options set from configuration providers:
                        { "HostId", "testhostidfromprovider" }
                    });
                })
                .ConfigureLogging(b =>
                {
                    b.SetMinimumLevel(LogLevel.Debug);
                    b.AddConsole();
                    b.AddApplicationInsights();
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
