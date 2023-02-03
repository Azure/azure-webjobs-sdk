// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Xunit;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests.Scale
{
    public class ScaleHostEndToEndTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ScaleManager_GetScaleStatusAsync_ReturnsExpected(bool tbsEnabled)
        {
            string triggerData =
@"[
{
  ""type"": ""serviceBusTrigger"",
  ""functionName"": ""Function1""
},
{
  ""type"": ""serviceBusTrigger"",
  ""functionName"": ""Function2""
},
{
  ""type"": ""cosmosDbTrigger"",
  ""functionName"": ""Function3""
}, 
]";

            string hostJson =
@"{
  ""extensions"": {
    ""serviceBus"" : {
      ""maxConcurrentCalls"": 16
    },
    ""cosmosDb"" : {
      ""maxConcurrentCalls"": 8
    }
  }
}";

            var loggerFactory = new LoggerFactory();
            var loggerProvider = new TestLoggerProvider();
            loggerFactory.AddProvider(loggerProvider);

            IHostBuilder hostBuilder = new HostBuilder();
            hostBuilder.ConfigureAppConfiguration((hostBuilderContext, config) =>
            {
                // Adding host.json here
                config.AddJsonStream(new MemoryStream(Encoding.ASCII.GetBytes(hostJson)));

                //Adding app setings
                config.AddInMemoryCollection(new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("app_setting1", "value1"),
                    new KeyValuePair<string, string>("app_setting2", "value2")
                });
            })
            .ConfigureServices(services =>
            {
                // Add all the services need to initinate IScaleManager and IScaleMonitorService
                services.AddSingleton<IScaleMonitorManager, ScaleMonitorManagerImpl>();
                services.AddSingleton<ITargetScalerManager, TargetScalerManagerImpl>();
                services.AddSingleton<IHostIdProvider>(new HostIdProviderImpl());
                services.AddSingleton<IConcurrencyStatusRepository, ConcurrencyStatusRepositoryImpl>();
                services.AddSingleton<IScaleMetricsRepository>(new ScaleMetricsRepositoryImpl());
                services.AddSingleton(loggerFactory);

                services.AddOptions<ScaleOptions>().Configure(options =>
                {
                    options.IsTargetBasedScalingEnabled = tbsEnabled;
                    options.IsTargetBasedScalingEnabledForTriggerFunc = (targetScaler) =>
                    {
                        return true;
                    };
                });
            })
            .AddScale(); // Adding IScaleManager and IScaleMonitorService

            // Iterate through triggers data and add scalers for each trigger
            var jarray = JArray.Parse(triggerData);
            foreach (var jtoken in jarray)
            {
                if (string.Equals(jtoken["type"].ToString(), "serviceBusTrigger", StringComparison.InvariantCultureIgnoreCase))
                {
                    hostBuilder.AddScaleForServciceBusTrigger(jtoken.ToString());
                }

                if (string.Equals(jtoken["type"].ToString(), "cosmosDbTrigger", StringComparison.InvariantCultureIgnoreCase))
                {
                    hostBuilder.AddScaleForCosmosDbTrigger(jtoken.ToString());
                }
            }

            IHost scaleHost = hostBuilder.Build();
            await scaleHost.StartAsync();

            IHostedService scaleMonitorService = scaleHost.Services.GetService<IHostedService>();
            Assert.NotNull(scaleMonitorService);

            await TestHelpers.Await(() =>
            {
                IScaleManager scaleMonitor = scaleHost.Services.GetService<IScaleManager>();
                var scaleStatus = scaleMonitor.GetScaleStatusAsync(new ScaleStatusContext()).GetAwaiter().GetResult();
                var scaleStatuses = scaleMonitor.GetScaleStatusesAsync(new ScaleStatusContext()).GetAwaiter().GetResult();
                if (!tbsEnabled)
                {
                    return scaleStatus.Vote == ScaleVote.ScaleOut && scaleStatus.TargetWorkerCount == null
                     && scaleStatuses["Function1"].Vote == ScaleVote.ScaleOut && scaleStatuses["Function1"].TargetWorkerCount == null
                     && scaleStatuses["Function2"].Vote == ScaleVote.ScaleOut && scaleStatuses["Function2"].TargetWorkerCount == null
                     && scaleStatuses["Function3"].Vote == ScaleVote.ScaleOut && scaleStatuses["Function3"].TargetWorkerCount == null;
                } 
                else
                {
                    return scaleStatus.Vote == ScaleVote.ScaleOut && scaleStatus.TargetWorkerCount == 4
                     && scaleStatuses["Function1"].Vote == ScaleVote.ScaleOut && scaleStatuses["Function1"].TargetWorkerCount == 2
                     && scaleStatuses["Function2"].Vote == ScaleVote.ScaleOut && scaleStatuses["Function2"].TargetWorkerCount == 3
                     && scaleStatuses["Function3"].Vote == ScaleVote.ScaleOut && scaleStatuses["Function3"].TargetWorkerCount == 4;
                }
            });
        }

        private class ScaleMonitorManagerImpl : IScaleMonitorManager
        {
            private readonly List<IScaleMonitor> _scalers;

            public ScaleMonitorManagerImpl(IEnumerable<IScaleMonitorProvider> providers)
            {
                _scalers = providers.Select(x => x.GetMonitor()).ToList();
            }

            public void Register(IScaleMonitor monitor)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<IScaleMonitor> GetMonitors()
            {
                return _scalers;
            }
        }

        private class TargetScalerManagerImpl : ITargetScalerManager
        {
            private readonly List<ITargetScaler> _scalers;

            public TargetScalerManagerImpl(IEnumerable<ITargetScalerProvider> providers)
            {
                _scalers = providers.Select(x => x.GetTargetScaler()).ToList();
            }

            public void Register(ITargetScaler monitor)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<ITargetScaler> GetTargetScalers()
            {
                return _scalers;
            }
        }

        private class HostIdProviderImpl : IHostIdProvider
        {
            public Task<string> GetHostIdAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult("test-host");
            }
        }

        private class ConcurrencyStatusRepositoryImpl : IConcurrencyStatusRepository
        {
            public Task WriteAsync(HostConcurrencySnapshot snapshot, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task<HostConcurrencySnapshot> ReadAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new HostConcurrencySnapshot()
                {
                    FunctionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>()
                    {
                        { "func1", new FunctionConcurrencySnapshot() { Concurrency = 1 } },
                        { "func2", new FunctionConcurrencySnapshot() { Concurrency = 1 } }
                    }
                });
            }
        }

        private class ScaleMetricsRepositoryImpl : IScaleMetricsRepository
        {
            private IDictionary<IScaleMonitor, ScaleMetrics> _cache;

            public Task WriteMetricsAsync(IDictionary<IScaleMonitor, ScaleMetrics> monitorMetrics)
            {
                _cache = monitorMetrics;
                return Task.CompletedTask;
            }

            public Task<IDictionary<IScaleMonitor, IList<ScaleMetrics>>> ReadMetricsAsync(IEnumerable<IScaleMonitor> monitors)
            {

                IDictionary<IScaleMonitor, IList<ScaleMetrics>> result = new Dictionary<IScaleMonitor, IList<ScaleMetrics>>();
                if (_cache != null)
                {
                    foreach (var pair in _cache)
                    {
                        result[pair.Key] = new List<ScaleMetrics>();
                    }
                }

                return Task.FromResult(result);
            }
        }

        private class ScaleMonitorImpl : IScaleMonitor
        {
            public ScaleMonitorDescriptor Descriptor { get; set; }

            public ScaleMonitorImpl(string id, string functionId)
            {
                Descriptor = new ScaleMonitorDescriptor(id, functionId);
            }

            public Task<ScaleMetrics> GetMetricsAsync()
            {
                return Task.FromResult(new ScaleMetrics());
            }

            public ScaleStatus GetScaleStatus(ScaleStatusContext context)
            {
                int targetWorkerCount = (int)Char.GetNumericValue(Descriptor.FunctionId[Descriptor.FunctionId.Length - 1]);

                return new ScaleStatus()
                {
                    TargetWorkerCount = targetWorkerCount,
                    Vote = ScaleVote.ScaleOut
                };
            }
        }

        private class TargetScalerImpl : ITargetScaler
        {
            public TargetScalerDescriptor TargetScalerDescriptor { get; set; }

            public TargetScalerImpl(string functionId)
            {
                TargetScalerDescriptor = new TargetScalerDescriptor(functionId);
            }

            public Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context)
            {
                int targetWorkerCount = (int)Char.GetNumericValue(TargetScalerDescriptor.FunctionId[TargetScalerDescriptor.FunctionId.Length - 1]) + 1;

                return Task.FromResult(new TargetScalerResult()
                {
                    TargetWorkerCount = targetWorkerCount
                });
            }
        }

        public class ScalerProvider : IScaleMonitorProvider, ITargetScalerProvider
        {
            private readonly string _functionName;

            public ScalerProvider(string functionName)
            {
                _functionName = functionName;
            }

            public IScaleMonitor GetMonitor()
            {
                return new ScaleMonitorImpl(_functionName, _functionName);
            }

            public ITargetScaler GetTargetScaler()
            {
                return new TargetScalerImpl(_functionName);
            }
        }
    }

    public static class ServiceBusHostBuilerExtension
    {
        public static void AddScaleForServciceBusTrigger(this IHostBuilder builder, string triggerData)
        {
            string fucntionName = JObject.Parse(triggerData)["functionName"].ToString();

            builder.ConfigureServices((context, services) =>
            {
                var appSetting = context.Configuration.GetValue<string>("app_setting1");
                Assert.NotNull(appSetting);
                var hostJsonSetting = context.Configuration.GetValue<int>("extensions:serviceBus:maxConcurrentCalls");
                Assert.NotNull(hostJsonSetting);

                var provider = new ScaleHostEndToEndTests.ScalerProvider(fucntionName);
                services.AddSingleton<IScaleMonitorProvider>(provider);
                services.AddSingleton<ITargetScalerProvider>(provider);
            });
        }
    }

    public static class CosmosDbHostBuilderExtension
    {
        public static void AddScaleForCosmosDbTrigger(this IHostBuilder builder, string triggerData)
        {
            string fucntionName = JObject.Parse(triggerData)["functionName"].ToString();

            builder.ConfigureServices(services =>
            {
                var provider = new ScaleHostEndToEndTests.ScalerProvider(fucntionName);
                services.AddSingleton<IScaleMonitorProvider>(provider);
                services.AddSingleton<ITargetScalerProvider>(provider);
            });
        }
    }
}
