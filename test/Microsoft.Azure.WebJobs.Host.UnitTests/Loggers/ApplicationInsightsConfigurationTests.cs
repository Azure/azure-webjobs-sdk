// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.Implementation.ApplicationId;
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
    public class ApplicationInsightsConfigurationTests : IDisposable
    {
        [Fact]
        public void DependencyInjectionConfiguration_Configures()
        {
            var builder = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsights(o => o.InstrumentationKey = "some key");
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
                Assert.Single(config.TelemetryInitializers.OfType<WebJobsSanitizingInitializer>());

                // Verify Channel
                Assert.IsType<ServerTelemetryChannel>(config.TelemetryChannel);

                var modules = host.Services.GetServices<ITelemetryModule>().ToList();

                // Verify Modules
                Assert.Equal(3, modules.Count);
                Assert.Single(modules.OfType<DependencyTrackingTelemetryModule>());
                Assert.Single(modules.OfType<QuickPulseTelemetryModule>());
                Assert.Single(modules.OfType<AppServicesHeartbeatTelemetryModule>());
                Assert.Same(config.TelemetryChannel, host.Services.GetServices<ITelemetryChannel>().Single());
                // Verify client
                var client = host.Services.GetService<TelemetryClient>();
                Assert.NotNull(client);
                Assert.True(client.Context.GetInternalContext().SdkVersion.StartsWith("webjobs"));

                // Verify provider
                var providers = host.Services.GetServices<ILoggerProvider>().ToList();
                Assert.Single(providers);
                Assert.IsType<ApplicationInsightsLoggerProvider>(providers[0]);
                Assert.NotNull(providers[0]);

                // Verify Processors
                Assert.Equal(3, config.TelemetryProcessors.Count);
                Assert.IsType<QuickPulseTelemetryProcessor>(config.TelemetryProcessors[0]);
                Assert.IsType<FilteringTelemetryProcessor>(config.TelemetryProcessors[1]);
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
                    b.AddApplicationInsights(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.SamplingSettings = samplingSettings;
                    });
                })
                .Build())
            {
                var config = host.Services.GetService<TelemetryConfiguration>();
                Assert.Equal(4, config.TelemetryProcessors.Count);
                Assert.IsType<QuickPulseTelemetryProcessor>(config.TelemetryProcessors[0]);
                Assert.IsType<FilteringTelemetryProcessor>(config.TelemetryProcessors[1]);
                Assert.IsType<AdaptiveSamplingTelemetryProcessor>(config.TelemetryProcessors[2]);

                Assert.Equal(samplingSettings.MaxTelemetryItemsPerSecond, ((AdaptiveSamplingTelemetryProcessor)config.TelemetryProcessors[2]).MaxTelemetryItemsPerSecond);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_NoFilterConfiguresSampling()
        {
            var samplingSettings = new SamplingPercentageEstimatorSettings { MaxTelemetryItemsPerSecond = 1 };
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsights(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.SamplingSettings = samplingSettings;
                    });
                }).Build())
            {
                var config = host.Services.GetService<TelemetryConfiguration>();
                Assert.Equal(4, config.TelemetryProcessors.Count);
                Assert.IsType<QuickPulseTelemetryProcessor>(config.TelemetryProcessors[0]);
                Assert.IsType<FilteringTelemetryProcessor>(config.TelemetryProcessors[1]);
                Assert.IsType<AdaptiveSamplingTelemetryProcessor>(config.TelemetryProcessors[2]);

                Assert.Equal(samplingSettings.MaxTelemetryItemsPerSecond, ((AdaptiveSamplingTelemetryProcessor)config.TelemetryProcessors[2]).MaxTelemetryItemsPerSecond);
            }
        }

        [Fact]
        public void DepednencyInjectionConfiguration_ConfiguresSnapshotCollector()
        {
            var snapshotConfiguration = new SnapshotCollectorConfiguration();
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsights(o =>
                    {
                        o.InstrumentationKey = "some key";
                        o.SnapshotConfiguration = snapshotConfiguration;
                    });
                }).Build())
            {
                var config = host.Services.GetService<TelemetryConfiguration>();
                Assert.Equal(4, config.TelemetryProcessors.Count);
                Assert.IsType<QuickPulseTelemetryProcessor>(config.TelemetryProcessors[0]);
                Assert.IsType<FilteringTelemetryProcessor>(config.TelemetryProcessors[1]);
                Assert.IsType<SnapshotCollectorTelemetryProcessor>(config.TelemetryProcessors[2]);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_ConfiguresActive()
        {
            using (var host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddApplicationInsights(o =>
                    {
                        o.InstrumentationKey = "some key";
                    });
                }).Build())
            {
                var config = TelemetryConfiguration.Active;
                // Verify Initializers
                Assert.Equal(5, config.TelemetryInitializers.Count);
                // These will throw if there are not exactly one
                Assert.Single(config.TelemetryInitializers.OfType<OperationCorrelationTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<HttpDependenciesParsingTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>());
                Assert.Single(config.TelemetryInitializers.OfType<WebJobsSanitizingInitializer>());

                // Verify Channel
                Assert.IsType<ServerTelemetryChannel>(config.TelemetryChannel);

                var modules = host.Services.GetServices<ITelemetryModule>().ToList();

                // Verify Modules
                Assert.Equal(3, modules.Count);
                Assert.Single(modules.OfType<DependencyTrackingTelemetryModule>());
                Assert.Single(modules.OfType<QuickPulseTelemetryModule>());
                Assert.Single(modules.OfType<AppServicesHeartbeatTelemetryModule>());
                Assert.NotSame(config.TelemetryChannel, host.Services.GetServices<ITelemetryChannel>().Single());
                // Verify client
                var client = host.Services.GetService<TelemetryClient>();
                Assert.NotNull(client);
                Assert.True(client.Context.GetInternalContext().SdkVersion.StartsWith("webjobs"));

                // Verify provider
                var providers = host.Services.GetServices<ILoggerProvider>().ToList();
                Assert.Single(providers);
                Assert.IsType<ApplicationInsightsLoggerProvider>(providers[0]);
                Assert.NotNull(providers[0]);

                // Verify Processors
                Assert.Equal(3, config.TelemetryProcessors.Count);
                Assert.IsType<QuickPulseTelemetryProcessor>(config.TelemetryProcessors[0]);
                Assert.IsType<FilteringTelemetryProcessor>(config.TelemetryProcessors[1]);
                Assert.Empty(config.TelemetryProcessors.OfType<AdaptiveSamplingTelemetryProcessor>());

                // Verify ApplicationIdProvider
                Assert.NotNull(config.ApplicationIdProvider);
                Assert.IsType<ApplicationInsightsApplicationIdProvider>(config.ApplicationIdProvider);
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
                    b.AddApplicationInsights(o =>
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

        public void Dispose()
        {
            TelemetryConfiguration.Active.Dispose();

            MethodInfo setActive =
                typeof(TelemetryConfiguration).GetMethod("set_Active", BindingFlags.Static | BindingFlags.NonPublic);

            setActive.Invoke(null, new object[] { TelemetryConfiguration.CreateDefault() });
        }
    }
}
