// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.EndToEndTests.Scale;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Xunit;
using static Microsoft.Azure.WebJobs.Host.EndToEndTests.Scale.ScaleHostEndToEndTests;

[assembly: WebJobsStartup(typeof(TestExtensionAStartup))]
[assembly: WebJobsStartup(typeof(TestExtensionBStartup))]

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.ScaleMonitoring)]
    public class ScaleHostEndToEndTests
    {
        private const string Function1Name = "Function1";
        private const string Function2Name = "Function2";
        private const string Function3Name = "Function3";

        internal const string ExtensionTypeA = "testExtensionATrigger";
        internal const string ExtensionTypeB = "testExtensionBTrigger";

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ScaleManager_GetScaleStatusAsync_ReturnsExpected(bool tbsEnabled)
        {
            var triggerMetadata = new List<TriggerMetadata>()
            {
                new TriggerMetadata(new JObject { { "functionName", $"{Function1Name}" }, { "type", $"{ExtensionTypeA}" } }),
                new TriggerMetadata(new JObject { { "functionName", $"{Function2Name}" }, { "type", $"{ExtensionTypeA}" } }),
                new TriggerMetadata(new JObject { { "functionName", $"{Function3Name}" }, { "type", $"{ExtensionTypeB}" } }, new Dictionary<string, object> { { "foo", "bar" } })
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

            IHostBuilder hostBuilder = new HostBuilder();
            hostBuilder.ConfigureLogging(configure =>
            {
                configure.SetMinimumLevel(LogLevel.Debug);
                configure.AddProvider(loggerProvider);
            });
            hostBuilder.ConfigureAppConfiguration((hostBuilderContext, config) =>
            {
                // Adding host.json here
                config.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(hostJson)));

                // Adding app settings
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "app_setting1", "value1" },
                    { "app_setting2", "value2"},
                    { "Microsoft.Azure.WebJobs.Host.EndToEndTests", "1" }
                });
            })
            .ConfigureServices(services =>
            {
                services.AddAzureClientsCore();
                services.AddAzureStorageScaleServices();

                services.AddSingleton<IConcurrencyStatusRepository, TestConcurrencyStatusRepository>();
                services.AddSingleton<ITriggerMetadataProvider>(new TestTriggerMetadataProvider(triggerMetadata));
            })
            .ConfigureWebJobsScale((context, builder) =>
            {
                builder.UseHostId(hostId);

                var webJobsBuilderContext = new WebJobsBuilderContext
                {
                    Configuration = context.Configuration,
                    EnvironmentName = context.HostingEnvironment.EnvironmentName
                };
                webJobsBuilderContext.Properties["IsAzureWebJobsScaleHost"] = true;
                builder.UseExternalStartup(new DefaultStartupTypeLocator(Assembly.GetExecutingAssembly()), webJobsBuilderContext, NullLoggerFactory.Instance);
            },
            scaleOptions =>
            {
                scaleOptions.IsTargetScalingEnabled = tbsEnabled;
                scaleOptions.MetricsPurgeEnabled = false;
                scaleOptions.ScaleMetricsMaxAge = TimeSpan.FromMinutes(4);
                scaleOptions.IsRuntimeScalingEnabled = true;
            });

            IHost scaleHost = hostBuilder.Build();
            await scaleHost.StartAsync();

            VerifyHostConfiguration(scaleHost);
            await VerifyScaleLogsAsync(scaleHost, tbsEnabled, loggerProvider);
        }

        private void VerifyHostConfiguration(IHost scaleHost)
        {
            IHostedService scaleMonitorService = scaleHost.Services.GetService<IHostedService>();
            Assert.NotNull(scaleMonitorService);

            var concurrencyStatusRepositories = scaleHost.Services.GetServices<IConcurrencyStatusRepository>().ToList();
            Assert.True(concurrencyStatusRepositories.Count == 2);
            // Validate that internal BlobStorageConcurrencyStatusRepository is available
            Assert.True(concurrencyStatusRepositories.SingleOrDefault(x => x.GetType().Name == "BlobStorageConcurrencyStatusRepository") != null);
            Assert.True(concurrencyStatusRepositories.SingleOrDefault(x => x is TestConcurrencyStatusRepository) != null);

            // Validate IConfiguration
            var config = scaleHost.Services.GetService<IConfiguration>();
            Assert.False(config.GetValue<string>("somevalue") == "1");
            Assert.True(config.GetValue<string>("Microsoft.Azure.WebJobs.Host.EndToEndTests") == "1");
            Assert.True(config.GetValue<string>("microsoft.azure.webJobs.host.endtoendtests") == "1");
        }

        private async Task VerifyScaleLogsAsync(IHost scaleHost, bool tbsEnabled, TestLoggerProvider loggerProvider)
        {
            await TestHelpers.Await(async () =>
            {
                IScaleStatusProvider scaleManager = scaleHost.Services.GetService<IScaleStatusProvider>();

                var scaleStatus = await scaleManager.GetScaleStatusAsync(new ScaleStatusContext());

                bool scaledOut = false;
                if (!tbsEnabled)
                {
                    scaledOut = scaleStatus.Vote == ScaleVote.ScaleOut && scaleStatus.TargetWorkerCount == null && scaleStatus.FunctionTargetScalerResults.Count == 0
                        && scaleStatus.FunctionScaleStatuses[Function1Name].Vote == ScaleVote.ScaleOut
                        && scaleStatus.FunctionScaleStatuses[Function2Name].Vote == ScaleVote.ScaleOut
                        && scaleStatus.FunctionScaleStatuses[Function3Name].Vote == ScaleVote.ScaleOut;

                    if (scaledOut)
                    {
                        var logMessages = loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage).ToArray();
                        Assert.Contains(logMessages, p => p.Contains("3 scale monitors to sample"));
                    }
                }
                else
                {
                    scaledOut = scaleStatus.Vote == ScaleVote.ScaleOut && scaleStatus.TargetWorkerCount == 4 && scaleStatus.FunctionScaleStatuses.Count == 0
                     && scaleStatus.FunctionTargetScalerResults[Function1Name].TargetWorkerCount == 2
                     && scaleStatus.FunctionTargetScalerResults[Function2Name].TargetWorkerCount == 3
                     && scaleStatus.FunctionTargetScalerResults[Function3Name].TargetWorkerCount == 4;

                    if (scaledOut)
                    {
                        var logMessages = loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage).ToArray();
                        Assert.Contains(logMessages, p => p.Contains("3 target scalers to sample"));
                    }
                }

                if (scaledOut)
                {
                    var logMessages = loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage).ToArray();
                    Assert.Contains(logMessages, p => p.Contains("Runtime scale monitoring is enabled."));
                    if (!tbsEnabled)
                    {
                        Assert.Contains(logMessages, p => p.Contains("Scaling out based on votes"));
                    }
                }

                return scaledOut;
            }, timeout: 5000, pollingInterval: 1000);
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
    }

    public class TestExtensionAStartup : IWebJobsStartup2
    {
        public void Configure(IWebJobsBuilder builder)
        {
        }

        public void Configure(WebJobsBuilderContext context, IWebJobsBuilder builder)
        {
            if (context.Properties.ContainsKey("IsAzureWebJobsScaleHost"))
            {
                builder.Services.AddSingleton<ITargetScalerCollectionProvider, TestExtensionATargetScalerCollectionProvider>();
                builder.Services.AddSingleton<IScaleMonitorCollectionProvider, TestExtensionAScaleMonitorCollectionProvider>();
            }
            else
            {
                Configure(builder);
            }
        }
    }

    public class TestExtensionBStartup : IWebJobsStartup2
    {
        public void Configure(IWebJobsBuilder builder)
        {
        }

        public void Configure(WebJobsBuilderContext context, IWebJobsBuilder builder)
        {
            if (context.Properties.ContainsKey("IsAzureWebJobsScaleHost"))
            {
                builder.Services.AddSingleton<ITargetScalerCollectionProvider, TestExtensionBTargetScalerCollectionProvider>();
                builder.Services.AddSingleton<IScaleMonitorCollectionProvider, TestExtensionBScaleMonitorCollectionProvider>();
            }
            else
            {
                Configure(builder);
            }
        }
    }

    internal class TestTriggerMetadataProvider : ITriggerMetadataProvider
    {
        private readonly IReadOnlyCollection<TriggerMetadata> _triggerMetadata;

        public TestTriggerMetadataProvider(List<TriggerMetadata> triggerMetadata)
        {
            _triggerMetadata = triggerMetadata.AsReadOnly();
        }

        public IEnumerable<TriggerMetadata> GetTriggerMetadata()
        {
            return _triggerMetadata;
        }
    }

    internal abstract class TestTargetScalerCollectionProvider : ITargetScalerCollectionProvider
    {
        private readonly TriggerMetadata[] _triggerMetadata;
        private readonly object _lock = new object();
        private ITargetScaler[] _targetScalers;

        public TestTargetScalerCollectionProvider(IConfiguration config, IOptions<ScaleOptions> scaleOptions, ITriggerMetadataProvider triggerMetadataProvider)
        {
            Assert.Equal(scaleOptions.Value.ScaleMetricsMaxAge, TimeSpan.FromMinutes(4));

            _triggerMetadata = triggerMetadataProvider.GetTriggerMetadata().Where(p => string.Compare(p.Type, ExtensionType, StringComparison.OrdinalIgnoreCase) == 0).ToArray();
        }

        public abstract string ExtensionType { get; }

        public IEnumerable<ITargetScaler> GetTargetScalers()
        {
            if (_targetScalers == null)
            {
                lock (_lock)
                {
                    if (_targetScalers == null)
                    {
                        List<ITargetScaler> targetScalers = new List<ITargetScaler>();
                        foreach (var triggerMetadata in _triggerMetadata)
                        {
                            var targetScaler = new TestTargetScaler(triggerMetadata.FunctionName);
                            targetScalers.Add(targetScaler);
                        }
                        _targetScalers = targetScalers.ToArray();
                    }
                }
            }
            return _targetScalers;
        }
    }

    internal abstract class TestScaleMonitorCollectionProvider : IScaleMonitorCollectionProvider
    {
        private readonly TriggerMetadata[] _triggerMetadata;
        private readonly object _lock = new object();
        private IScaleMonitor[] _scaleMonitors;

        public TestScaleMonitorCollectionProvider(IConfiguration config, IOptions<ScaleOptions> scaleOptions, ITriggerMetadataProvider triggerMetadataProvider)
        {
            Assert.Equal(scaleOptions.Value.ScaleMetricsMaxAge, TimeSpan.FromMinutes(4));

            _triggerMetadata = triggerMetadataProvider.GetTriggerMetadata().Where(p => string.Compare(p.Type, ExtensionType, StringComparison.OrdinalIgnoreCase) == 0).ToArray();
        }

        public abstract string ExtensionType { get; }

        public IEnumerable<IScaleMonitor> GetScaleMonitors()
        {
            if (_scaleMonitors == null)
            {
                lock (_lock)
                {
                    if (_scaleMonitors == null)
                    {
                        List<IScaleMonitor> scaleMonitors = new List<IScaleMonitor>();
                        foreach (var triggerMetadata in _triggerMetadata)
                        {
                            var monitor = new TestScaleMonitor(triggerMetadata.FunctionName, triggerMetadata.FunctionName);
                            scaleMonitors.Add(monitor);
                        }
                        _scaleMonitors = scaleMonitors.ToArray();
                    }
                }
            }
            return _scaleMonitors;
        }
    }

    internal class TestExtensionATargetScalerCollectionProvider : TestTargetScalerCollectionProvider
    {
        public TestExtensionATargetScalerCollectionProvider(IConfiguration config, IOptions<ScaleOptions> scaleOptions, ITriggerMetadataProvider triggerMetadataProvider)
            : base(config, scaleOptions, triggerMetadataProvider)
        {
            // verify we can access configuration settings
            var appSetting = config.GetValue<string>("app_setting1");
            Assert.NotNull(appSetting);
            var hostJsonSetting = config.GetValue<int>("extensions:testExtensionA:foo");
            Assert.NotNull(hostJsonSetting);
        }

        public override string ExtensionType => ExtensionTypeA;
    }

    internal class TestExtensionAScaleMonitorCollectionProvider : TestScaleMonitorCollectionProvider
    {
        public TestExtensionAScaleMonitorCollectionProvider(IConfiguration config, IOptions<ScaleOptions> scaleOptions, ITriggerMetadataProvider triggerMetadataProvider)
            : base(config, scaleOptions, triggerMetadataProvider)
        {
            // verify we can access configuration settings
            var appSetting = config.GetValue<string>("app_setting1");
            Assert.NotNull(appSetting);
            var hostJsonSetting = config.GetValue<int>("extensions:testExtensionA:foo");
            Assert.NotNull(hostJsonSetting);
        }

        public override string ExtensionType => ExtensionTypeA;
    }

    internal class TestExtensionBTargetScalerCollectionProvider : TestTargetScalerCollectionProvider
    {
        public TestExtensionBTargetScalerCollectionProvider(IConfiguration config, IOptions<ScaleOptions> scaleOptions, ITriggerMetadataProvider triggerMetadataProvider)
            : base(config, scaleOptions, triggerMetadataProvider)
        {
        }

        public override string ExtensionType => ExtensionTypeB;
    }

    internal class TestExtensionBScaleMonitorCollectionProvider : TestScaleMonitorCollectionProvider
    {
        public TestExtensionBScaleMonitorCollectionProvider(IConfiguration config, IOptions<ScaleOptions> scaleOptions, ITriggerMetadataProvider triggerMetadataProvider)
            : base(config, scaleOptions, triggerMetadataProvider)
        {
        }

        public override string ExtensionType => ExtensionTypeB;
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
}
