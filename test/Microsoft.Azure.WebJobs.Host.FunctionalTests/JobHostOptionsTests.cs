// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class JobHostOptionsTests
    {
        [Fact]
        public void UseEnvironment_Development_DefaultsOptionsCorrectly()
        {
            var hostBuilder = new HostBuilder()
                .UseEnvironment("Development")
                .ConfigureDefaultTestHost(b =>
                {
                    b.AddAzureStorage()
                    .AddAzureStorageCoreServices();
                });

            IHost host = hostBuilder.Build();
            var config = host.Services.GetService<DistributedLockManagerContainerProvider>();

            var hostingEnvironment = host.Services.GetService<IHostingEnvironment>();
            Assert.True(hostingEnvironment.IsDevelopment());

            var queuesOptions = host.Services.GetService<IOptions<QueuesOptions>>();
            Assert.Equal(TimeSpan.FromSeconds(2), queuesOptions.Value.MaxPollingInterval);

            var singletonOptions = host.Services.GetService< IOptions<SingletonOptions>>();
            Assert.Equal(TimeSpan.FromSeconds(15), singletonOptions.Value.ListenerLockPeriod);
        }

        [Fact]
        public void UseEnvironment_Production_DoesNotDefaultsOptions()
        {
            var hostBuilder = new HostBuilder()
                .UseEnvironment("Production")
                .ConfigureDefaultTestHost(b =>
                {
                    b.AddAzureStorage()
                    .AddAzureStorageCoreServices();
                });

            IHost host = hostBuilder.Build();
            var config = host.Services.GetService<DistributedLockManagerContainerProvider>();

            var hostingEnvironment = host.Services.GetService<IHostingEnvironment>();
            Assert.False(hostingEnvironment.IsDevelopment());
            Assert.True(hostingEnvironment.IsProduction());

            var queuesOptions = host.Services.GetService<IOptions<QueuesOptions>>();
            Assert.Equal(TimeSpan.FromMinutes(1), queuesOptions.Value.MaxPollingInterval);

            var singletonOptions = host.Services.GetService<IOptions<SingletonOptions>>();
            Assert.Equal(TimeSpan.FromSeconds(60), singletonOptions.Value.ListenerLockPeriod);
        }

        [Fact]
        public void UseEnvironment_Development_UserConfigTakesPrecedence()
        {
            var hostBuilder = new HostBuilder()
                .UseEnvironment("Development")
                .ConfigureDefaultTestHost(b =>
                {
                    b.AddAzureStorage()
                    .AddAzureStorageCoreServices();
                })
                .ConfigureServices(services =>
                {
                    services.Configure<QueuesOptions>(options =>
                    {
                        options.MaxPollingInterval = TimeSpan.FromSeconds(22);
                    });

                    services.Configure<SingletonOptions>(options =>
                    {
                        options.ListenerLockPeriod = TimeSpan.FromSeconds(22);
                    });
                });

            IHost host = hostBuilder.Build();
            var config = host.Services.GetService<DistributedLockManagerContainerProvider>();

            var hostingEnvironment = host.Services.GetService<IHostingEnvironment>();
            Assert.True(hostingEnvironment.IsDevelopment());

            var queuesOptions = host.Services.GetService<IOptions<QueuesOptions>>();
            Assert.Equal(TimeSpan.FromSeconds(22), queuesOptions.Value.MaxPollingInterval);

            var singletonOptions = host.Services.GetService<IOptions<SingletonOptions>>();
            Assert.Equal(TimeSpan.FromSeconds(22), singletonOptions.Value.ListenerLockPeriod);
        }

        private class FastLogger : IAsyncCollector<FunctionInstanceLogEntry>, IEventCollectorFactory
        {
            public List<FunctionInstanceLogEntry> List = new List<FunctionInstanceLogEntry>();

            public static FunctionInstanceLogEntry FlushEntry = new FunctionInstanceLogEntry(); // marker for flushes

            public Task AddAsync(FunctionInstanceLogEntry item, CancellationToken cancellationToken = default(CancellationToken))
            {
                if (item.Arguments == null)
                {
                    return Task.CompletedTask;
                }
                var clone = JsonConvert.DeserializeObject<FunctionInstanceLogEntry>(JsonConvert.SerializeObject(item));
                List.Add(clone);
                return Task.CompletedTask;
            }

            public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                List.Add(FlushEntry);
                return Task.CompletedTask;
            }

            public IAsyncCollector<FunctionInstanceLogEntry> Create()
            {
                return this;
            }
        }

        // Verify that JobHostConfig pulls a Sas container from appsettings. 
        // Tests reading from an environment variable.
        [Fact]
        public void JobHost_UsesSas_FromEnvVar()
        {
            var fakeSasUri = "https://contoso.blob.core.windows.net/myContainer?signature=foo";

            using (EnvVarHolder.Set("AzureWebJobs:InternalSasBlobContainer", fakeSasUri))
            {
                var hostBuilder = new HostBuilder()
                 .ConfigureDefaultTestHost(b =>
                    {
                        b.AddAzureStorage()
                        .AddAzureStorageCoreServices();
                    })
                 .ConfigureAppConfiguration(c =>
                 {
                     c.AddEnvironmentVariables();
                 });

                IHost host = hostBuilder.Build();

                var config = host.Services.GetService<DistributedLockManagerContainerProvider>();

                var container = config.InternalContainer;
                Assert.NotNull(container);
                Assert.Equal(container.Name, "myContainer"); // specified in sas. 
            }
        }

        // Verify that JobHostConfig pulls a Sas container from appsettings. 
        [Fact]
        public void JobHost_UsesSas_SetService()
        {
            var hostBuilder = new HostBuilder()
             .ConfigureDefaultTestHost(b =>
             {
                 b.AddAzureStorage()
                 .AddAzureStorageCoreServices();
             })
             .ConfigureAppConfiguration(c =>
             {
             });

            // Explicitly set the service
            hostBuilder.ConfigureServices((ctx, services) =>
            {
                var uri2 = new Uri("https://contoso.blob.core.windows.net/myContainer2?signature=foo");
                services.AddSingleton<DistributedLockManagerContainerProvider>(new DistributedLockManagerContainerProvider()
                {
                    InternalContainer = new WindowsAzure.Storage.Blob.CloudBlobContainer(uri2)
                });
            });

            IHost host = hostBuilder.Build();

            var config = host.Services.GetService<DistributedLockManagerContainerProvider>();

            var container = config.InternalContainer;
            Assert.NotNull(container);
            Assert.Equal(container.Name, "myContainer2"); // specified in sas. 
        }

        // Verify that JobHostConfig pulls a Sas container from appsettings. 
        [Fact]
        public void JobHost_UsesSas()
        {
            var fakeSasUri = "https://contoso.blob.core.windows.net/myContainer3?signature=foo";

            var hostBuilder = new HostBuilder()
             .ConfigureDefaultTestHost(b =>
             {
                 b.AddAzureStorage()
                 .AddAzureStorageCoreServices();
             })
             .ConfigureAppConfiguration(c =>
             {
                 c.AddInMemoryCollection(new Dictionary<string, string>
                 {
                        { "AzureWebJobs:InternalSasBlobContainer", fakeSasUri }
                 });
             });

            IHost host = hostBuilder.Build();

            var config = host.Services.GetService<DistributedLockManagerContainerProvider>();

            var container = config.InternalContainer;
            Assert.NotNull(container);
            Assert.Equal(container.Name, "myContainer3"); // specified in sas.             
        }

        // Test that we can explicitly disable storage and call through a function
        // And enable the fast table logger and ensure that's getting events.
        [Fact]
        public async Task JobHost_NoStorage_Succeeds()
        {
            var fastLogger = new FastLogger();

            using (EnvVarHolder.Set("AzureWebJobsStorage", null))
            using (EnvVarHolder.Set("AzureWebJobsDashboard", null))
            {

                IHost host = new HostBuilder()
                    .ConfigureWebJobs(b =>
                    {
                        b.AddTableLogging(fastLogger);
                    })
                    .ConfigureTypeLocator(typeof(BasicTest))
                    .Build();

                var randomValue = Guid.NewGuid().ToString();

                StringBuilder sbLoggingCallbacks = new StringBuilder();

                // Manually invoked.
                var method = typeof(BasicTest).GetMethod("Method", BindingFlags.Public | BindingFlags.Static);

                var lockManager = host.Services.GetRequiredService<IDistributedLockManager>();
                Assert.IsType<InMemoryDistributedLockManager>(lockManager);

                await host.GetJobHost().CallAsync(method, new { value = randomValue });
                Assert.True(BasicTest.Called);

                Assert.Equal(2, fastLogger.List.Count); // We should be batching, so flush not called yet.

                host.Start(); // required to call stop()
                await host.StopAsync(); // will ensure flush is called.

                // Verify fast logs
                Assert.Equal(3, fastLogger.List.Count);

                var startMsg = fastLogger.List[0];
                Assert.Equal("BasicTest.Method", startMsg.FunctionName);
                Assert.Equal(null, startMsg.EndTime);
                Assert.NotNull(startMsg.StartTime);

                var endMsg = fastLogger.List[1];
                Assert.Equal(startMsg.FunctionName, endMsg.FunctionName);
                Assert.Equal(startMsg.StartTime, endMsg.StartTime);
                Assert.Equal(startMsg.FunctionInstanceId, endMsg.FunctionInstanceId);
                Assert.NotNull(endMsg.EndTime); // signal completed
                Assert.True(endMsg.StartTime <= endMsg.EndTime);
                Assert.Null(endMsg.ErrorDetails);
                Assert.Null(endMsg.ParentId);

                Assert.Equal(2, endMsg.Arguments.Count);
                Assert.True(endMsg.Arguments.ContainsKey("log"));
                Assert.Equal(randomValue, endMsg.Arguments["value"]);

                Assert.Equal("val=" + randomValue, endMsg.LogOutput.Trim());

                Assert.Same(FastLogger.FlushEntry, fastLogger.List[2]);
            } // EnvVarHolder
        }

        public class BasicTest
        {
            public static bool Called = false;

            [NoAutomaticTrigger]
            public static void Method(TextWriter log, string value)
            {
                log.Write("val={0}", value);
                Called = true;
            }
        }
    }
}