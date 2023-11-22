// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using Azure.Identity;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.Implementation.ApplicationId;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.ApplicationInsights.WindowsServer;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class ApplicationInsightsConfigurationTests
    {
        public ApplicationInsightsConfigurationTests()
        {
            TelemetryConfiguration.Active.InstrumentationKey = "";

            var initializers = new List<ITelemetryInitializer>(TelemetryConfiguration.Active.TelemetryInitializers);

            foreach (var i in initializers)
            {
                if (!(i is OperationCorrelationTelemetryInitializer))
                {
                    TelemetryConfiguration.Active.TelemetryInitializers.Remove(i);
                }
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_Configures_With_InstrumentationKey()
        {
            var builder = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o => o.InstrumentationKey = "some key");
                });

            using (var host = builder.Build())
            {
                var config = host.Services.GetService<TelemetryConfiguration>();

                // Verify Initializers
                Assert.Equal(5, config.TelemetryInitializers.Count);
                // These will throw if there are not exactly one
                Assert.Single(config.TelemetryInitializers.OfType<OperationCorrelationTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<HttpDependenciesParsingTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<MetricSdkVersionTelemetryInitializer>());

                var sdkVersionProvider = host.Services.GetServices<ISdkVersionProvider>().ToList();
                Assert.Single(sdkVersionProvider);
                Assert.Single(sdkVersionProvider.OfType<WebJobsSdkVersionProvider>());

                var roleInstanceProvider = host.Services.GetServices<IRoleInstanceProvider>().ToList();
                Assert.Single(roleInstanceProvider);
                Assert.Single(roleInstanceProvider.OfType<WebJobsRoleInstanceProvider>());

                // Verify Channel
                Assert.IsType<ServerTelemetryChannel>(config.TelemetryChannel);

                var modules = host.Services.GetServices<ITelemetryModule>().ToList();

                // Verify Modules
                Assert.Equal(6, modules.Count);
                Assert.Single(modules.OfType<DependencyTrackingTelemetryModule>());

                Assert.Single(modules.OfType<QuickPulseTelemetryModule>());
                Assert.Single(modules.OfType<PerformanceCollectorModule>());
                Assert.Single(modules.OfType<AppServicesHeartbeatTelemetryModule>());
                Assert.Single(modules.OfType<RequestTrackingTelemetryModule>());
                // SelfDiagnosticsTelemetryModule is disabled by default and instead NullTelemetryModule is added
                Assert.Single(modules.OfType<NullTelemetryModule>());

                var dependencyModule = modules.OfType<DependencyTrackingTelemetryModule>().Single();

                Assert.Equal(ActivityIdFormat.W3C, Activity.DefaultIdFormat);
                Assert.True(Activity.ForceDefaultIdFormat);

                Assert.Same(config.TelemetryChannel, host.Services.GetServices<ITelemetryChannel>().Single());
                // Verify client
                var client = host.Services.GetService<TelemetryClient>();
                Assert.NotNull(client);
                Assert.StartsWith("webjobs", client.Context.GetInternalContext().SdkVersion);

                // Verify provider
                var providers = host.Services.GetServices<ILoggerProvider>().ToList();
                Assert.Single(providers);
                Assert.IsType<ApplicationInsightsLoggerProvider>(providers[0]);
                Assert.NotNull(providers[0]);

                // Verify Processors
                Assert.Equal(4, config.TelemetryProcessors.Count);
                Assert.IsType<OperationFilteringTelemetryProcessor>(config.TelemetryProcessors[0]);
                Assert.IsType<QuickPulseTelemetryProcessor>(config.TelemetryProcessors[1]);
                Assert.IsType<FilteringTelemetryProcessor>(config.TelemetryProcessors[2]);
                Assert.Empty(config.TelemetryProcessors.OfType<AdaptiveSamplingTelemetryProcessor>());

                // Verify ApplicationIdProvider
                Assert.NotNull(config.ApplicationIdProvider);
                Assert.IsType<ApplicationInsightsApplicationIdProvider>(config.ApplicationIdProvider);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_Configures_With_ConnectionString()
        {
            var builder = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.ConnectionString = "InstrumentationKey=somekey;EndpointSuffix=applicationinsights.us";
                        o.DiagnosticsEventListenerLogLevel = EventLevel.Verbose;
                        o.EnableAutocollectedMetricsExtractor = true;
                    }); 
                });

            using (var host = builder.Build())
            {
                var config = host.Services.GetService<TelemetryConfiguration>();

                Assert.Equal("somekey", config.InstrumentationKey);

                // Verify Initializers
                Assert.Equal(5, config.TelemetryInitializers.Count);
                // These will throw if there are not exactly one
                Assert.Single(config.TelemetryInitializers.OfType<OperationCorrelationTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<HttpDependenciesParsingTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<MetricSdkVersionTelemetryInitializer>());

                var sdkVersionProvider = host.Services.GetServices<ISdkVersionProvider>().ToList();
                Assert.Single(sdkVersionProvider);
                Assert.Single(sdkVersionProvider.OfType<WebJobsSdkVersionProvider>());

                var roleInstanceProvider = host.Services.GetServices<IRoleInstanceProvider>().ToList();
                Assert.Single(roleInstanceProvider);
                Assert.Single(roleInstanceProvider.OfType<WebJobsRoleInstanceProvider>());

                // Verify Channel
                Assert.IsType<ServerTelemetryChannel>(config.TelemetryChannel);
                Assert.Contains("applicationinsights.us", config.TelemetryChannel.EndpointAddress);

                var modules = host.Services.GetServices<ITelemetryModule>().ToList();

                // Verify Modules
                Assert.Equal(6, modules.Count);
                Assert.Single(modules.OfType<DependencyTrackingTelemetryModule>());

                Assert.Single(modules.OfType<QuickPulseTelemetryModule>());
                Assert.Single(modules.OfType<PerformanceCollectorModule>());
                Assert.Single(modules.OfType<AppServicesHeartbeatTelemetryModule>());
                Assert.Single(modules.OfType<RequestTrackingTelemetryModule>());
                Assert.Single(modules.OfType<SelfDiagnosticsTelemetryModule>());

                var dependencyModule = modules.OfType<DependencyTrackingTelemetryModule>().Single();

                Assert.Equal(ActivityIdFormat.W3C, Activity.DefaultIdFormat);
                Assert.True(Activity.ForceDefaultIdFormat);

                Assert.Same(config.TelemetryChannel, host.Services.GetServices<ITelemetryChannel>().Single());
                // Verify client
                var client = host.Services.GetService<TelemetryClient>();
                Assert.NotNull(client);
                Assert.StartsWith("webjobs", client.Context.GetInternalContext().SdkVersion);

                // Verify provider
                var providers = host.Services.GetServices<ILoggerProvider>().ToList();
                Assert.Single(providers);
                Assert.IsType<ApplicationInsightsLoggerProvider>(providers[0]);
                Assert.NotNull(providers[0]);

                // Verify Processors
                Assert.Equal(5, config.TelemetryProcessors.Count);
                Assert.IsType<AutocollectedMetricsExtractor>(config.TelemetryProcessors[0]);
                Assert.IsType<OperationFilteringTelemetryProcessor>(config.TelemetryProcessors[1]);
                Assert.IsType<QuickPulseTelemetryProcessor>(config.TelemetryProcessors[2]);
                Assert.IsType<FilteringTelemetryProcessor>(config.TelemetryProcessors[3]);
                Assert.Empty(config.TelemetryProcessors.OfType<AdaptiveSamplingTelemetryProcessor>());

                // Verify ApplicationIdProvider
                Assert.NotNull(config.ApplicationIdProvider);
                Assert.IsType<ApplicationInsightsApplicationIdProvider>(config.ApplicationIdProvider);
                Assert.Contains("applicationinsights.us", ((ApplicationInsightsApplicationIdProvider)config.ApplicationIdProvider).ProfileQueryEndpoint);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_ConfiguresSampling()
        {
            var samplingSettings = new SamplingPercentageEstimatorSettings { MaxTelemetryItemsPerSecond = 1 };
            var samplingExcludedTypes = "PageView;Request";
            var samplingIncludedTypes = "Trace";
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.SamplingSettings = samplingSettings;
                        o.SamplingExcludedTypes = samplingExcludedTypes;
                        o.SamplingIncludedTypes = samplingIncludedTypes;
                    });
                })
                .Build())
            {
                var config = host.Services.GetService<TelemetryConfiguration>();
                Assert.Equal(5, config.TelemetryProcessors.Count);
                Assert.IsType<OperationFilteringTelemetryProcessor>(config.TelemetryProcessors[0]);
                Assert.IsType<QuickPulseTelemetryProcessor>(config.TelemetryProcessors[1]);
                Assert.IsType<FilteringTelemetryProcessor>(config.TelemetryProcessors[2]);
                Assert.IsType<AdaptiveSamplingTelemetryProcessor>(config.TelemetryProcessors[3]);

                Assert.Equal(samplingSettings.MaxTelemetryItemsPerSecond, ((AdaptiveSamplingTelemetryProcessor)config.TelemetryProcessors[3]).MaxTelemetryItemsPerSecond);
                Assert.Equal(samplingExcludedTypes, ((AdaptiveSamplingTelemetryProcessor)config.TelemetryProcessors[3]).ExcludedTypes);
                Assert.Equal(samplingIncludedTypes, ((AdaptiveSamplingTelemetryProcessor)config.TelemetryProcessors[3]).IncludedTypes);
            }
        }

        
        [Fact]
        public void DependencyInjectionConfiguration_EnableLiveMetricsFilters()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.EnableLiveMetricsFilters = true;
                    });
                })
                .Build())
            {
                var config = host.Services.GetService<TelemetryConfiguration>();
                Assert.Equal(3, config.TelemetryProcessors.Count);
                Assert.IsType<OperationFilteringTelemetryProcessor>(config.TelemetryProcessors[0]);
                Assert.IsType<QuickPulseTelemetryProcessor>(config.TelemetryProcessors[1]);
              }
        }

        [Fact]
        public void DependencyInjectionConfiguration_ConfiguresRequestCollectionOptions()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                    });
                })
                .Build())
            {
                var modules = host.Services.GetServices<ITelemetryModule>().ToList();
                var requestModule = modules.OfType<RequestTrackingTelemetryModule>().Single();

                Assert.True(requestModule.CollectionOptions.InjectResponseHeaders);
                Assert.False(requestModule.CollectionOptions.TrackExceptions);
                Assert.Equal(ActivityIdFormat.W3C, Activity.DefaultIdFormat);
                Assert.True(Activity.ForceDefaultIdFormat);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_NoClientIpInitializerWithoutContextAccessor()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o => { o.InstrumentationKey = "some key"; });
                })
                .Build())
            {
                var config = host.Services.GetServices<TelemetryConfiguration>().Single();
                Assert.DoesNotContain(config.TelemetryInitializers, ti => ti is ClientIpHeaderTelemetryInitializer);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_NoClientIpInitializerWithoutExtendedHttpOptions()
        {
            using (var host = new HostBuilder()
                .ConfigureServices(b =>
                    b.AddHttpContextAccessor())
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection = false;
                    });
                })
                .Build())
            {
                var config = host.Services.GetServices<TelemetryConfiguration>().Single();
                Assert.DoesNotContain(config.TelemetryInitializers, ti => ti is ClientIpHeaderTelemetryInitializer);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_ConfiguresClientIpInitializer()
        {
            using (var host = new HostBuilder()
                .ConfigureServices(b =>
                    b.AddHttpContextAccessor())
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                    });
                })
                .Build())
            {
                var config = host.Services.GetServices<TelemetryConfiguration>().Single();

                Assert.Contains(config.TelemetryInitializers, ti => ti is ClientIpHeaderTelemetryInitializer);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_ConfiguresCustomRequestCollectionOptions()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.HttpAutoCollectionOptions.EnableW3CDistributedTracing = false;
                        o.HttpAutoCollectionOptions.EnableResponseHeaderInjection = false;
                    });
                })
                .Build())
            {
                var modules = host.Services.GetServices<ITelemetryModule>().ToList();
                var dependencyModule = modules.OfType<DependencyTrackingTelemetryModule>().Single();
                var requestModule = modules.OfType<RequestTrackingTelemetryModule>().Single();

                Assert.False(requestModule.CollectionOptions.InjectResponseHeaders);
                Assert.False(requestModule.CollectionOptions.TrackExceptions);
                Assert.Equal(ActivityIdFormat.Hierarchical, Activity.DefaultIdFormat);
                Assert.True(Activity.ForceDefaultIdFormat);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_DisableHttpRequestCollectionOptions()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection = false;
                        o.HttpAutoCollectionOptions.EnableW3CDistributedTracing = false;
                    });
                })
                .Build())
            {
                var modules = host.Services.GetServices<ITelemetryModule>().ToList();
                var dependencyModule = modules.OfType<DependencyTrackingTelemetryModule>().Single();
                var requestModules = modules.OfType<RequestTrackingTelemetryModule>();

                Assert.False(dependencyModule.EnableW3CHeadersInjection);
                Assert.Empty(requestModules);

                Assert.Equal(ActivityIdFormat.Hierarchical, Activity.DefaultIdFormat);
                Assert.True(Activity.ForceDefaultIdFormat);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_DisablesPerformanceCounters()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.EnablePerformanceCountersCollection = false;
                    });
                })
                .Build())
            {
                var modules = host.Services.GetServices<ITelemetryModule>();
                Assert.True(modules.Count(m => m is PerformanceCollectorModule) == 0);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_DisablesDependencyTracking()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsights(o =>
                    {
                        o.InstrumentationKey = "KI";
                        o.EnableDependencyTracking = false;
                    });
                })
                .Build())
            {
                var modules = host.Services.GetServices<ITelemetryModule>();
                Assert.True(modules.Count(m => m is DependencyTrackingTelemetryModule) == 0);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_EnableSqlCommandTextInstrumentation()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsights(o =>
                    {
                        o.InstrumentationKey = "KI";
                        o.EnableDependencyTracking = true;
                        o.DependencyTrackingOptions = new DependencyTrackingOptions() { EnableSqlCommandTextInstrumentation = true };
                    });
                })
                .Build())
            {
                var modules = host.Services.GetServices<ITelemetryModule>();
                var matchingModules = modules.Where(m => m is DependencyTrackingTelemetryModule);
                Assert.True(matchingModules.Count() == 1);

                var module = matchingModules.First() as DependencyTrackingTelemetryModule;
                Assert.True(module.EnableSqlCommandTextInstrumentation);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_SqlCommandTextInstrumentation_DisabledByDefault()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsights(o =>
                    {
                        o.InstrumentationKey = "KI";
                        o.EnableDependencyTracking = false;
                        o.DependencyTrackingOptions = new DependencyTrackingOptions() { EnableSqlCommandTextInstrumentation = true };
                    });
                })
                .Build())
            {
                var modules = host.Services.GetServices<ITelemetryModule>();
                Assert.True(modules.Count(m => m is DependencyTrackingTelemetryModule) == 0);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_DisablesQuickPulseTelemetry()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsights(o =>
                    {
                        o.InstrumentationKey = "LOKI";
                        o.EnableLiveMetrics = false;
                    });
                })
                .Build())
            {
                var modules = host.Services.GetServices<ITelemetryModule>();
                Assert.True(modules.Count(m => m is QuickPulseTelemetryModule) == 0);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_EnableTelemetryModulesByDefault()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsights(o =>
                    {
                        o.InstrumentationKey = "LOKI";
                    });
                })
                .Build())
            {
                var modules = host.Services.GetServices<ITelemetryModule>();
                Assert.True(modules.Count(m => m is QuickPulseTelemetryModule) == 1);
                Assert.True(modules.Count(m => m is DependencyTrackingTelemetryModule) == 1);
                Assert.True(modules.Count(m => m is PerformanceCollectorModule) == 1);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_ConfiguresQuickPulseAuthApiKeyDeprecated()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.QuickPulseAuthenticationApiKey = "some auth key";  // This is deprecated, but still supported
                    });
                })
                .Build())
            {
                var modules = host.Services.GetServices<ITelemetryModule>().ToList();
                var quickPulseTelemetryModule = modules.OfType<QuickPulseTelemetryModule>().Single();
                Assert.Equal("some auth key", quickPulseTelemetryModule.AuthenticationApiKey);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_ConfiguresLiveMetricsAuthApiKey()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.LiveMetricsAuthenticationApiKey = "some auth key";
                    });
                })
                .Build())
            {
                var modules = host.Services.GetServices<ITelemetryModule>().ToList();
                var quickPulseTelemetryModule = modules.OfType<QuickPulseTelemetryModule>().Single();
                Assert.Equal("some auth key", quickPulseTelemetryModule.AuthenticationApiKey);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_ConfiguresLiveMetricsServerId()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.Services.AddSingleton<IRoleInstanceProvider>(new TestRoleInstanceProvider("my role instance"));
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                    });
                })
                .Build())
            {
                var modules = host.Services.GetServices<ITelemetryModule>().ToList();
                var quickPulseTelemetryModule = modules.OfType<QuickPulseTelemetryModule>().Single();
                Assert.Equal("my role instance", quickPulseTelemetryModule.ServerId);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_NoFilterConfiguresSampling()
        {
            var samplingSettings = new SamplingPercentageEstimatorSettings { MaxTelemetryItemsPerSecond = 1 };
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.SamplingSettings = samplingSettings;
                    });
                }).Build())
            {
                var config = host.Services.GetService<TelemetryConfiguration>();
                Assert.Equal(5, config.TelemetryProcessors.Count);
                Assert.IsType<OperationFilteringTelemetryProcessor>(config.TelemetryProcessors[0]);
                Assert.IsType<QuickPulseTelemetryProcessor>(config.TelemetryProcessors[1]);
                Assert.IsType<FilteringTelemetryProcessor>(config.TelemetryProcessors[2]);
                Assert.IsType<AdaptiveSamplingTelemetryProcessor>(config.TelemetryProcessors[3]);

                Assert.Equal(samplingSettings.MaxTelemetryItemsPerSecond, ((AdaptiveSamplingTelemetryProcessor)config.TelemetryProcessors[3]).MaxTelemetryItemsPerSecond);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_ConfiguresSnapshotCollector()
        {
            var snapshotConfiguration = new SnapshotCollectorConfiguration();
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.SnapshotConfiguration = snapshotConfiguration;
                    });
                }).Build())
            {
                var config = host.Services.GetService<TelemetryConfiguration>();
                Assert.Equal(5, config.TelemetryProcessors.Count);
                Assert.IsType<OperationFilteringTelemetryProcessor>(config.TelemetryProcessors[0]);
                Assert.IsType<QuickPulseTelemetryProcessor>(config.TelemetryProcessors[1]);
                Assert.IsType<FilteringTelemetryProcessor>(config.TelemetryProcessors[2]);
                Assert.IsType<SnapshotCollectorTelemetryProcessor>(config.TelemetryProcessors[3]);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_ConfiguresActive()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                    });
                }).Build())
            {
                // Verify Initializers
                Assert.Equal(3, TelemetryConfiguration.Active.TelemetryInitializers.Count);

                // These will throw if there are not exactly one
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<OperationCorrelationTelemetryInitializer>());
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>());
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>());

                // ikey should still be set
                Assert.Equal("some key", TelemetryConfiguration.Active.InstrumentationKey);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_ConfiguresActiveEvenIfIKeySetUp()
        {
            TelemetryConfiguration.Active.InstrumentationKey = "some other ikey";
            using (new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                    });
                }).Build())
            {
                // Verify Initializers
                Assert.Equal(3, TelemetryConfiguration.Active.TelemetryInitializers.Count);

                // These will throw if there are not exactly one
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<OperationCorrelationTelemetryInitializer>());
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>());
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>());
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_ConfiguresActiveOnlyOnce()
        {
            using (var _ = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                    });
                }).Build())
            {
            }

            // TelemetryConfiguration.Active is a static singleton
            // so it persist after host is disposed
            using (var host2 = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                    });
                }).Build())
            {
                // Verify Initializers
                Assert.Equal(3, TelemetryConfiguration.Active.TelemetryInitializers.Count);

                // These will throw if there are not exactly one
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<OperationCorrelationTelemetryInitializer>());
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>());
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>());

                // ikey should still be set
                Assert.Equal("some key", TelemetryConfiguration.Active.InstrumentationKey);
            }
        }

        [Fact]
        public void CreateFilterOptions_EnableLiveMetricsFilters()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.EnableLiveMetricsFilters = true;
                    });
                })
                .Build())
            {
                var filterOptions = host.Services.GetService<IOptions<LoggerFilterOptions>>().Value;
                var rule = SelectAppInsightsRule(filterOptions, "Category");
                Assert.Equal(null, rule.Filter);
            }
        }

        [Fact]
        public void CreateFilterOptions_DisableLiveMetrics()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.EnableLiveMetrics = false;
                    });
                })
                .Build())
            {
                var filterOptions = host.Services.GetService<IOptions<LoggerFilterOptions>>().Value;
                var rule = SelectAppInsightsRule(filterOptions, "Category");
                Assert.Equal(null, rule.Filter);
            }
        }

        [Fact]
        public void CreateFilterOptions_MinLevel()
        {
            using (var host = CreateHost((c, b) => b.SetMinimumLevel(LogLevel.None)))
            {
                var options = GetInternalLoggerFilterOptions(host.Services);
                var rule = SelectAppInsightsRule(options, "Any");
                Assert.Equal(LogLevel.None, rule.LogLevel);
            }
        }

        [Fact]
        public void CreateFilterOptions_CategoryFilter()
        {
            // Only return true if the category matches
            using (var host = CreateHost((c, b) => b.AddFilter("MyFakeCategory", LogLevel.Error)))
            {
                var options = GetInternalLoggerFilterOptions(host.Services);
                var rule = SelectAppInsightsRule(options, "MyFakeCategory");
                Assert.Equal(LogLevel.Error, rule.LogLevel);

                // Make sure the default still applies to other categories
                rule = SelectAppInsightsRule(options, "AnotherCategory");
                Assert.Equal(LogLevel.Information, rule.LogLevel);
            }
        }

        [Fact]
        public void CreateFilterOptions_CustomFilter()
        {
            // Only return true if the category matches
            using (var host = CreateHost((c, b) => b.AddFilter((cat, l) => cat == "MyFakeCategory")))
            {
                var options = GetInternalLoggerFilterOptions(host.Services);
                var rule = SelectAppInsightsRule(options, null);
                Assert.Null(rule.LogLevel);
                Assert.False(rule.Filter(null, "SomeOtherCategory", LogLevel.Information));
                Assert.True(rule.Filter(null, "MyFakeCategory", LogLevel.Information));
            }
        }

        [Fact]
        public void CreateFilterOptions_AppInsightsFilter()
        {
            // Make sure we allow custom filters for our logger
            using (var host = CreateHost((c, b) => b.AddFilter<ApplicationInsightsLoggerProvider>((cat, l) => cat == "MyFakeCategory")))
            {
                var options = GetInternalLoggerFilterOptions(host.Services);
                var rule = SelectAppInsightsRule(options, null);
                Assert.Null(rule.LogLevel);
                Assert.False(rule.Filter(null, "SomeOtherCategory", LogLevel.Information));
                Assert.True(rule.Filter(null, "MyFakeCategory", LogLevel.Information));
            }
        }

        [Fact]
        public void CreateFilterOptions_AppInsightsMinLevel()
        {
            // Make sure we allow custom filters for our logger
            using (var host = CreateHost((c, b) => b.AddFilter<ApplicationInsightsLoggerProvider>("MyFakeCategory", LogLevel.Critical)))
            {
                var options = GetInternalLoggerFilterOptions(host.Services);
                var rule = SelectAppInsightsRule(options, "MyFakeCategory");
                Assert.Equal(LogLevel.Critical, rule.LogLevel);
            }
        }

        [Fact]
        public void CreateFilterOptions_AppInsightsMultipleCustomFilters()
        {
            using (var host = CreateHost((c, b) =>
            {
                // Last one should win
                b.AddFilter<ApplicationInsightsLoggerProvider>((cat, l) => cat == "1");
                b.AddFilter<ApplicationInsightsLoggerProvider>((cat, l) => cat == "2");
                b.AddFilter<ApplicationInsightsLoggerProvider>((cat, l) => cat == "3");
            }))
            {
                var options = GetInternalLoggerFilterOptions(host.Services);
                var rule = SelectAppInsightsRule(options, "MyFakeCategory");

                Assert.Null(rule.LogLevel);
                Assert.False(rule.Filter(null, "1", LogLevel.Critical));
                Assert.False(rule.Filter(null, "2", LogLevel.Critical));
                Assert.True(rule.Filter(null, "3", LogLevel.Critical));
            }
        }

        [Theory]
        [InlineData("Logging")]
        [InlineData("Multiple:Sections")]
        public void Filter_BindsToConfiguration(string configSection)
        {
            using (var host = CreateHost(
                configureLogging: (c, b) =>
                {
                    // This is how logging config sections are registered by applications
                    b.AddConfiguration(c.Configuration.GetSection(configSection));
                },
                configureConfiguration: b =>
                {
                    b.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { $"{configSection}:{ApplicationInsightsLoggerProvider.Alias}:LogLevel:Default", LogLevel.Warning.ToString() }
                    });
                }))
            {
                var options = GetInternalLoggerFilterOptions(host.Services);
                var rule = SelectAppInsightsRule(options, "MyFakeCategory");
                Assert.Equal(LogLevel.Warning, rule.LogLevel);
            }
        }

        private static IHost CreateHost(Action<HostBuilderContext, ILoggingBuilder> configureLogging = null, Action<IConfigurationBuilder> configureConfiguration = null)
        {
            var builder = new HostBuilder()
                .ConfigureWebJobs()
                .ConfigureLogging((c, b) =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                    });

                    configureLogging?.Invoke(c, b);
                });

            if (configureConfiguration != null)
            {
                builder.ConfigureAppConfiguration(configureConfiguration);
            }

            return builder.Build();
        }

        private static LoggerFilterOptions GetInternalLoggerFilterOptions(IServiceProvider services)
        {
            var filterOptions = services.GetService<IOptions<LoggerFilterOptions>>().Value;

            // The previous options will set the level to Trace
            var rule = SelectAppInsightsRule(filterOptions, "UnknownCategory");
            Assert.Equal(LogLevel.Trace, rule.LogLevel);

            // These are the options to be used by the filtering processor
            var internalOptions = ApplicationInsightsServiceCollectionExtensions.CreateFilterOptions(filterOptions);
            Assert.NotSame(filterOptions, internalOptions);

            return internalOptions;
        }

        // Helper to pull out the calculated rule
        private static LoggerFilterRule SelectAppInsightsRule(LoggerFilterOptions options, string category)
        {
            var providerType = typeof(ApplicationInsightsLoggerProvider);

            var ruleSelector = new LoggerRuleSelector();
            ruleSelector.Select(options, providerType, category, out LogLevel? minLevel, out Func<string, string, LogLevel, bool> filter);

            return new LoggerFilterRule(providerType.FullName, category, minLevel, filter);
        }

        class TestRoleInstanceProvider : IRoleInstanceProvider
        {
            private readonly string _roleInstance;

            public TestRoleInstanceProvider(string roleInstance)
            {
                _roleInstance = roleInstance;
            }

            public string GetRoleInstanceName()
            {
                return _roleInstance;
            }
        }

        [Fact]
        public void ManagedIdentityCredential()
        {
            var builder = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o => { o.InstrumentationKey = "some key"; o.TokenCredentialOptions = Logging.ApplicationInsights.TokenCredentialOptions.ParseAuthenticationString($"Authorization=AAD;ClientId={Guid.NewGuid()}"); });
                });

            using (var host = builder.Build())
            {
                var config = host.Services.GetService<TelemetryConfiguration>();
                
                var property = typeof(TelemetryConfiguration).GetProperty("CredentialEnvelope", BindingFlags.NonPublic | BindingFlags.Instance);
                var propertyValue = property.GetValue(config);

                var credentialProperty = propertyValue.GetType().GetProperty("Credential", BindingFlags.NonPublic | BindingFlags.Instance);
                var credentialValue = credentialProperty.GetValue(propertyValue);                
                Assert.IsType<ManagedIdentityCredential>(credentialValue);
            }
        }       

        [Fact]
        public void DefaultAuth()
        {
            var builder = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                    });
                });

            using (var host = builder.Build())
            {
                var config = host.Services.GetService<TelemetryConfiguration>();
                var property = typeof(TelemetryConfiguration).GetProperty("CredentialEnvelope", BindingFlags.NonPublic | BindingFlags.Instance);
                var propertyValue = property.GetValue(config);
                Assert.Null(propertyValue);
            }
        }
    }
}
