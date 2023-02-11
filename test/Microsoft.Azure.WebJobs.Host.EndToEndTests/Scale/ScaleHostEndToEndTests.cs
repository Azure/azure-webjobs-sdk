// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests.Scale
{
    public class ScaleHostEndToEndTests
    {
        private const string Function1Name = "Function1";
        private const string Function2Name = "Function2";
        private const string Function3Name = "Function3";

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ScaleManager_GetScaleStatusAsync_ReturnsExpected(bool tbsEnabled)
        {
            JArray functionMetadata = new JArray
            {
                new JObject
                {
                    { "type", "testExtensionATrigger" },
                    { "functionName", Function1Name }
                },
                new JObject
                {
                    { "type", "testExtensionATrigger" },
                    { "functionName", Function2Name }
                },
                new JObject
                {
                    { "type", "testExtensionBTrigger" },
                    { "functionName", Function3Name }
                }
            };

            string hostJson =
            @"{
              ""extensions"": {
                ""testExtensionA"" : {
                  ""foo"": 16
                },
                ""testExtensionB"" : {
                  ""bar"": 8
                }
              }
            }";

            string hostId = "test-host";
            var loggerProvider = new TestLoggerProvider();

            // We want to associate an object instance with each TriggerMetdata,
            // for example AzureComponentFactory can be specified as a TriggerMetdata property as it requires a new refference we do not want
            Dictionary<string, IEnumerable<object>> properties = new Dictionary<string, IEnumerable<object>>();
            foreach (var function in functionMetadata)
            {
                if (function["type"].ToString() == "testExtensionBTrigger")
                {
                    properties.Add(function["functionName"].ToString(), new List<object> { new string("some_property") });
                }
            }
            ITriggerMetadataProvider triggerMetadataProvider = new TriggerMetadataProvider(functionMetadata, properties);

            IHostBuilder hostBuilder = new HostBuilder();
            hostBuilder.ConfigureLogging(configure =>
            {
                configure.SetMinimumLevel(LogLevel.Debug);
                configure.AddProvider(loggerProvider);
            });
            hostBuilder.ConfigureAppConfiguration((hostBuilderContext, config) =>
            {
                // Adding host.json here
                config.AddJsonStream(new MemoryStream(Encoding.ASCII.GetBytes(hostJson)));

                // Adding app settings
                config.AddInMemoryCollection(new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("app_setting1", "value1"),
                    new KeyValuePair<string, string>("app_setting2", "value2")
                });

                // Adding hosting config features
                config.AddInMemoryCollection(new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>($"{Constants.HostingConfigSectionName}:Microsoft.Azure.WebJobs.Host.EndToEndTests", "1"),
                });
            })
            .ConfigureServices(service =>
            {
                // IConcurrencyStatusRepository needs AzureCompoentFactory
                service.AddAzureClientsCore();
                // Adding IConcurrencyStatusRepository/IAzureBlobStorageProvider
                service.AddAzureStorageScaleServices(options =>
                {
                    options.InternalSasBlobContainer = "sas-blob-container";
                });

                // replace IConcurrencyStatusRepository for the test
                service.AddSingleton<IConcurrencyStatusRepository, TestConcurrencyStatusRepository>();
            })
            .ConfigureWebJobsScale<TestScaleMetricsRepository>(options =>
            {
                // configure scale options
                options.IsTargetScalingEnabled = tbsEnabled;
                options.MetricsPurgeEnabled = false;
            }, hostId, triggerMetadataProvider)
            .ConfigureTestExtensionAScale()
            .ConfigureTestExtensionBScale();

            IHost scaleHost = hostBuilder.Build();
            await scaleHost.StartAsync();

            IHostedService scaleMonitorService = scaleHost.Services.GetService<IHostedService>();
            Assert.NotNull(scaleMonitorService);

            var concurrencyStatusRepositories = scaleHost.Services.GetServices<IConcurrencyStatusRepository>().ToList();
            Assert.True(concurrencyStatusRepositories.Count == 2);
            // Validate that internal BlobStorageConcurrencyStatusRepository is available
            Assert.True(concurrencyStatusRepositories.SingleOrDefault(x => x.GetType().Name == "BlobStorageConcurrencyStatusRepository") != null);
            Assert.True(concurrencyStatusRepositories.SingleOrDefault(x => x is TestConcurrencyStatusRepository) != null);

            await TestHelpers.Await(async () =>
            {
                IScaleManager scaleManager = scaleHost.Services.GetService<IScaleManager>();

                var scaleStatus = await scaleManager.GetScaleStatusAsync(new ScaleStatusContext());
                var functionScaleStatuses = scaleStatus.FunctionScaleStatuses;

                bool scaledOut = false;
                if (!tbsEnabled)
                {
                    scaledOut = scaleStatus.Vote == ScaleVote.ScaleOut && scaleStatus.TargetWorkerCount == null
                     && functionScaleStatuses[Function1Name].Vote == ScaleVote.ScaleOut && functionScaleStatuses[Function1Name].TargetWorkerCount == null
                     && functionScaleStatuses[Function2Name].Vote == ScaleVote.ScaleOut && functionScaleStatuses[Function2Name].TargetWorkerCount == null
                     && functionScaleStatuses[Function3Name].Vote == ScaleVote.ScaleOut && functionScaleStatuses[Function3Name].TargetWorkerCount == null;

                    if (scaledOut)
                    {
                        var logMessages = loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage).ToArray();
                        Assert.Contains(logMessages, p => p.Contains("3 scale monitors to sample"));
                    }
                }
                else
                {
                    scaledOut = scaleStatus.Vote == ScaleVote.ScaleOut && scaleStatus.TargetWorkerCount == 4
                     && functionScaleStatuses[Function1Name].Vote == ScaleVote.ScaleOut && functionScaleStatuses[Function1Name].TargetWorkerCount == 2
                     && functionScaleStatuses[Function2Name].Vote == ScaleVote.ScaleOut && functionScaleStatuses[Function2Name].TargetWorkerCount == 3
                     && functionScaleStatuses[Function3Name].Vote == ScaleVote.ScaleOut && functionScaleStatuses[Function3Name].TargetWorkerCount == 4;

                    if (scaledOut)
                    {
                        var logMessages = loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage).ToArray();
                        Assert.Contains(logMessages, p => p.Contains("3 target scalers to sample"));
                    }
                }

                if (scaledOut)
                {
                    var logMessages = loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage).ToArray();
                    Assert.Contains(logMessages, p => p.Contains("Scale monitor service started is started."));
                    Assert.Contains(logMessages, p => p.Contains("Scaling out based on votes"));
                }

                return scaledOut;
            });
        }

        private class TestConcurrencyStatusRepository : IConcurrencyStatusRepository
        {
            public TestConcurrencyStatusRepository()
            {
            }

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
                        { Function1Name, new FunctionConcurrencySnapshot() { Concurrency = 1 } },
                        { Function2Name, new FunctionConcurrencySnapshot() { Concurrency = 1 } }
                    }
                });
            }
        }

        private class TestScaleMetricsRepository : IScaleMetricsRepository
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
    }

    internal abstract class TestExtensionScalerProvider : IScalerProvider
    {
        private IOptions<ScaleOptions> _scaleOptions;
        private List<IScaleMonitor> _scaleMonitors;
        private List<ITargetScaler> _targetScalers;
        private ITriggerMetadataProvider _triggerMetadataProvider;

        public TestExtensionScalerProvider(IConfiguration config, IOptions<ScaleOptions> scaleOptions, ITriggerMetadataProvider triggerMetadataProvider)
        {
            _scaleOptions = scaleOptions;
            _triggerMetadataProvider = triggerMetadataProvider;
        }

        protected abstract string[] TriggerTypes { get; }

        public IEnumerable<IScaleMonitor> GetScaleMonitors()
        {
            if (_scaleMonitors == null)
            {
                var triggersMetadata = TriggerTypes.SelectMany(x => _triggerMetadataProvider.GetTriggersMetadata(x));
                _scaleMonitors = new List<IScaleMonitor>();
                foreach (var triggerMetadata in triggersMetadata)
                {
                    string functionName = triggerMetadata.Value["functionName"].ToString();
                    _scaleMonitors.Add(new TestScaleMonitor(functionName, functionName));
                }
            }
            return _scaleMonitors.AsReadOnly();
        }

        public IEnumerable<ITargetScaler> GetTargetScalers()
        {
            if (_targetScalers == null)
            {
                var triggersMetadata = TriggerTypes.SelectMany(x => _triggerMetadataProvider.GetTriggersMetadata(x));
                _targetScalers = new List<ITargetScaler>();
                foreach (var triggerData in triggersMetadata)
                {
                    string functionName = triggerData.Value["functionName"].ToString();
                    _targetScalers.Add(new TestTargetScaler(functionName));
                }
            }
            return _targetScalers.AsReadOnly();
        }
    }

    public static class TestExtensionAHostBuilderExtensions
    {
        public static IHostBuilder ConfigureTestExtensionAScale(this IHostBuilder builder)
        {
            builder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<IScalerProvider, TestExtensionAScalerProvider>();
            });

            return builder;
        }

        private class TestExtensionAScalerProvider : TestExtensionScalerProvider
        {
            public TestExtensionAScalerProvider(IConfiguration config, IOptions<ScaleOptions> scaleOptions, ITriggerMetadataProvider triggerMetadataProvider)
                : base(config, scaleOptions, triggerMetadataProvider)
            {
                // verify we can access configuration settings
                var appSetting = config.GetValue<string>("app_setting1");
                Assert.NotNull(appSetting);
                var hostJsonSetting = config.GetValue<int>("extensions:testExtensionA:foo");
                Assert.NotNull(hostJsonSetting);

                // verify we can not access a trigger metadata property
                var triggersMetadata = TriggerTypes.SelectMany(x => triggerMetadataProvider.GetTriggersMetadata(x)).ToArray();
                Assert.True(string.IsNullOrEmpty(triggersMetadata[0].GetProperty<string>()));
            }

            protected override string[] TriggerTypes => new string[] { "testExtensionATrigger" };
        }
    }

    public static class TestExtensionBHostBuilderExtensions
    {
        public static IHostBuilder ConfigureTestExtensionBScale(this IHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IScalerProvider, TestExtensionBScalerProvider>();
            });

            return builder;
        }

        private class TestExtensionBScalerProvider : TestExtensionScalerProvider
        {
            public TestExtensionBScalerProvider(IConfiguration config, IOptions<ScaleOptions> scaleOptions, ITriggerMetadataProvider triggerMetadataProvider)
                : base(config, scaleOptions, triggerMetadataProvider)
            {
                // verify we can access a trigger metadata property
                var triggersMetadata = TriggerTypes.SelectMany(x => triggerMetadataProvider.GetTriggersMetadata(x)).ToArray();
                Assert.Equal(triggersMetadata[0].GetProperty<string>(), "some_property");
            }

            protected override string[] TriggerTypes => new string[] { "testExtensionBTrigger" };
        }
    }

    internal class TestScaleMonitor : IScaleMonitor
    {
        public ScaleMonitorDescriptor Descriptor { get; set; }

        public TestScaleMonitor(string id, string functionId)
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

    internal class TestTargetScaler : ITargetScaler
    {
        public TargetScalerDescriptor TargetScalerDescriptor { get; set; }

        public TestTargetScaler(string functionId)
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

    internal class FakeAzureComponentFactory
    {
    }
}
