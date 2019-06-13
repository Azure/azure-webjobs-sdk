// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.Implementation.ApplicationId;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.ApplicationInsights.Extensibility.W3C;
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
        public void DependencyInjectionConfiguration_Configures()
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
                Assert.Equal(7, config.TelemetryInitializers.Count);
                // These will throw if there are not exactly one
                Assert.Single(config.TelemetryInitializers.OfType<OperationCorrelationTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<HttpDependenciesParsingTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<WebJobsSanitizingInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<MetricSdkVersionTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<W3COperationCorrelationTelemetryInitializer>());

                var sdkVersionProvider = host.Services.GetServices<ISdkVersionProvider>().ToList();
                Assert.Single(sdkVersionProvider);
                Assert.Single(sdkVersionProvider.OfType<WebJobsSdkVersionProvider>());

                // Verify Channel
                Assert.IsType<ServerTelemetryChannel>(config.TelemetryChannel);

                var modules = host.Services.GetServices<ITelemetryModule>().ToList();

                // Verify Modules
                Assert.Equal(5, modules.Count);
                Assert.Single(modules.OfType<DependencyTrackingTelemetryModule>());

                Assert.Single(modules.OfType<QuickPulseTelemetryModule>());
                Assert.Single(modules.OfType<PerformanceCollectorModule>());
                Assert.Single(modules.OfType<AppServicesHeartbeatTelemetryModule>());
                Assert.Single(modules.OfType<RequestTrackingTelemetryModule>());

                var dependencyModule = modules.OfType<DependencyTrackingTelemetryModule>().Single();
                Assert.True(dependencyModule.EnableW3CHeadersInjection);

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
        public void DependencyInjectionConfiguration_ConfiguresSampling()
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
                var dependencyModule = modules.OfType<DependencyTrackingTelemetryModule>().Single();
                var requestModule = modules.OfType<RequestTrackingTelemetryModule>().Single();

                Assert.True(dependencyModule.EnableW3CHeadersInjection);
                Assert.True(requestModule.CollectionOptions.EnableW3CDistributedTracing);
                Assert.True(requestModule.CollectionOptions.InjectResponseHeaders);
                Assert.False(requestModule.CollectionOptions.TrackExceptions);
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

                Assert.False(dependencyModule.EnableW3CHeadersInjection);
                Assert.False(requestModule.CollectionOptions.EnableW3CDistributedTracing);
                Assert.False(requestModule.CollectionOptions.InjectResponseHeaders);
                Assert.False(requestModule.CollectionOptions.TrackExceptions);
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
        public void DependencyInjectionConfiguration_ConfiguresQuickPulseAuthApiKey()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.QuickPulseAuthenticationApiKey = "some auth key";
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
        public void DepednencyInjectionConfiguration_ConfiguresSnapshotCollector()
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
                Assert.Equal(4, TelemetryConfiguration.Active.TelemetryInitializers.Count);

                // These will throw if there are not exactly one
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<OperationCorrelationTelemetryInitializer>());
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<W3COperationCorrelationTelemetryInitializer>());
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
                Assert.Equal(4, TelemetryConfiguration.Active.TelemetryInitializers.Count);

                // These will throw if there are not exactly one
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<OperationCorrelationTelemetryInitializer>());
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>());
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>());
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<W3COperationCorrelationTelemetryInitializer>());
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

            // TelemteryConfiguration.Active is a static singleton
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
                Assert.Equal(4, TelemetryConfiguration.Active.TelemetryInitializers.Count);

                // These will throw if there are not exactly one
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<OperationCorrelationTelemetryInitializer>());
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>());
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>());
                Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers.OfType<W3COperationCorrelationTelemetryInitializer>());

                // ikey should still be set
                Assert.Equal("some key", TelemetryConfiguration.Active.InstrumentationKey);
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
    }
}
