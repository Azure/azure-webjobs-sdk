// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Xunit;
using static Microsoft.Azure.WebJobs.Host.EndToEndTests.Scale.ScaleHostEndToEndTests;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.ScaleMonitoring)]
    public class ScaleHostEndToEndTests
    {
        private const string Function1Name = "Function1";
        private const string Function2Name = "Function2";
        private const string Function3Name = "Function3";

        private const string ATriggerType = "testExtensionATrigger";
        private const string BTriggerType = "testExtensionBTrigger";

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ScaleManager_GetScaleStatusAsync_ReturnsExpected(bool tbsEnabled)
        {
            var triggerMetadata = new List<TriggerMetadata>()
            {
                new TriggerMetadata(new JObject { { "functionName", $"{Function1Name}" }, { "type", $"{ATriggerType}" } }),
                new TriggerMetadata(new JObject { { "functionName", $"{Function2Name}" }, { "type", $"{ATriggerType}" } }),
                new TriggerMetadata(new JObject { { "functionName", $"{Function3Name}" }, { "type", $"{BTriggerType}" } }, new Dictionary<string, object> { { "foo", "bar" } })
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
            })
            .ConfigureWebJobsScale((context, builder) =>
                {
                    builder.UseHostId(hostId);
                },
                scaleOptions =>
                {
                    scaleOptions.IsTargetScalingEnabled = tbsEnabled;
                    scaleOptions.MetricsPurgeEnabled = false;
                    scaleOptions.ScaleMetricsMaxAge = TimeSpan.FromMinutes(4);
                    scaleOptions.IsRuntimeScalingEnabled = true;
                });

            foreach (TriggerMetadata t in triggerMetadata)
            {
                if (t.Type == ATriggerType)
                {
                    hostBuilder.ConfigureTestExtensionAScale(t);
                }
                else
                {
                    hostBuilder.ConfigureTestExtensionBScale(t);
                }
            }

            IHost scaleHost = hostBuilder.Build();
            await scaleHost.StartAsync();

            IHostedService scaleMonitorService = scaleHost.Services.GetService<IHostedService>();
            Assert.NotNull(scaleMonitorService);

            var concurrencyStatusRepositories = scaleHost.Services.GetServices<IConcurrencyStatusRepository>().ToList();
            Assert.True(concurrencyStatusRepositories.Count == 2);
            // Validate that internal BlobStorageConcurrencyStatusRepository is available
            Assert.True(concurrencyStatusRepositories.SingleOrDefault(x => x.GetType().Name == "BlobStorageConcurrencyStatusRepository") != null);
            Assert.True(concurrencyStatusRepositories.SingleOrDefault(x => x is TestConcurrencyStatusRepository) != null);

            // Validate IConfiguration
            var config = scaleHost.Services.GetService<IConfiguration>();
            Assert.False(config.GetValue<string>("sovemalue") == "1");
            Assert.True(config.GetValue<string>("Microsoft.Azure.WebJobs.Host.EndToEndTests") == "1");
            Assert.True(config.GetValue<string>("microsoft.azure.webJobs.host.endtoendtests") == "1");

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
            }, pollingInterval: 1000);
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

        internal abstract class TestExtensionScalerProvider : IScaleMonitorProvider, ITargetScalerProvider
        {
            private IOptions<ScaleOptions> _scaleOptions;
            private IScaleMonitor _scaleMonitor;
            private ITargetScaler _targetScaler;
            private TriggerMetadata _triggerMetadata;

            public TestExtensionScalerProvider(IConfiguration config, IOptions<ScaleOptions> scaleOptions, TriggerMetadata triggerMetadata)
            {
                Assert.Equal(scaleOptions.Value.ScaleMetricsMaxAge, TimeSpan.FromMinutes(4));

                _scaleOptions = scaleOptions;
                _triggerMetadata = triggerMetadata;
            }

            public IScaleMonitor GetMonitor()
            {
                if (_scaleMonitor == null)
                {
                    _scaleMonitor = new TestScaleMonitor(_triggerMetadata.FunctionName, _triggerMetadata.FunctionName);
                }
                return _scaleMonitor;
            }

            public ITargetScaler GetTargetScaler()
            {
                if (_targetScaler == null)
                {
                    _targetScaler = new TestTargetScaler(_triggerMetadata.FunctionName);
                }
                return _targetScaler;
            }
        }
    }
    public static class TestExtensionAHostBuilderExtensions
    {
        public static IHostBuilder ConfigureTestExtensionAScale(this IHostBuilder builder, TriggerMetadata triggerMetadata)
        {
            builder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<IScaleMonitorProvider>(serviceProvider =>
                {
                    IConfiguration config = serviceProvider.GetService<IConfiguration>();
                    IOptions<ScaleOptions> scaleOptions = serviceProvider.GetService<IOptions<ScaleOptions>>();
                    return new TestExtensionAScalerProvider(config, scaleOptions, triggerMetadata);
                });
                services.AddSingleton<ITargetScalerProvider>(serviceProvider =>
                {
                    IConfiguration config = serviceProvider.GetService<IConfiguration>();
                    IOptions<ScaleOptions> scaleOptions = serviceProvider.GetService<IOptions<ScaleOptions>>();
                    return new TestExtensionAScalerProvider(config, scaleOptions, triggerMetadata);
                });
            });

            return builder;
        }

        private class TestExtensionAScalerProvider : TestExtensionScalerProvider
        {
            public TestExtensionAScalerProvider(IConfiguration config, IOptions<ScaleOptions> scaleOptions, TriggerMetadata triggerMetadata)
                : base(config, scaleOptions, triggerMetadata)
            {
                // verify we can access configuration settings
                var appSetting = config.GetValue<string>("app_setting1");
                Assert.NotNull(appSetting);
                var hostJsonSetting = config.GetValue<int>("extensions:testExtensionA:foo");
                Assert.NotNull(hostJsonSetting);
            }
        }
    }

    public static class TestExtensionBHostBuilderExtensions
    {
        public static IHostBuilder ConfigureTestExtensionBScale(this IHostBuilder builder, TriggerMetadata triggerMetadata)
        {
            builder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<IScaleMonitorProvider>(serviceProvider =>
                {
                    IConfiguration config = serviceProvider.GetService<IConfiguration>();
                    IOptions<ScaleOptions> scaleOptions = serviceProvider.GetService<IOptions<ScaleOptions>>();
                    return new TestExtensionBScalerProvider(config, scaleOptions, triggerMetadata);
                });
                services.AddSingleton<ITargetScalerProvider>(serviceProvider =>
                {
                    IConfiguration config = serviceProvider.GetService<IConfiguration>();
                    IOptions<ScaleOptions> scaleOptions = serviceProvider.GetService<IOptions<ScaleOptions>>();
                    return new TestExtensionBScalerProvider(config, scaleOptions, triggerMetadata);
                });
            });

            return builder;
        }

        private class TestExtensionBScalerProvider : TestExtensionScalerProvider
        {
            public TestExtensionBScalerProvider(IConfiguration config, IOptions<ScaleOptions> scaleOptions, TriggerMetadata triggerMetadata)
                : base(config, scaleOptions, triggerMetadata)
            {
            }
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
