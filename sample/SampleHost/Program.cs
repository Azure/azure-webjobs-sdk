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
            var x = new ServiceCollection(); 

            var builder = new HostBuilder()
                .UseEnvironment("Development")
                .ConfigureWebJobsHost(o =>
                {
                    // Example setting options properties:
                    // o.HostId = "testhostid";
                })
                // These can be toggled independently!
                .AddWebJobsLogging()    // Enables WebJobs v1 classic logging 
                .AddStorageForRuntimeInternals() // enables WebJobs to run distributed, via a storage account to coordinate
                .AddStorageBindings()   // adds [Blob], etc bindings for Azure Storage. 
                .AddApplicationInsights()
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
                })
                .UseConsoleLifetime();

            var jobHost = builder.Build();

            var opts = jobHost.Services.GetRequiredService<IOptions<LegacyConfig>>();

            using (jobHost)
            {
                await jobHost.RunAsync();
            }
        }
    }
}
