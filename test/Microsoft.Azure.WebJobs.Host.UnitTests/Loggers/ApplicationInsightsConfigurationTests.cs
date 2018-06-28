// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.WindowsServer;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class ApplicationInsightsConfigurationTests : IDisposable
    {
        [Fact]
        public void DependencyInjectionConfiguration_Configures()
        {
            using (var host = new HostBuilder().AddApplicationInsights("some key", (c, l) => true, null).Build())
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
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_ConfiguresSampling()
        {
            var samplingSettings = new SamplingPercentageEstimatorSettings {MaxTelemetryItemsPerSecond = 1};
            using (var host = new HostBuilder()
                .AddApplicationInsights("some key", (c, l) => true, samplingSettings)
                .Build())
            {
                var config = host.Services.GetService<TelemetryConfiguration>();
                Assert.Equal(4, config.TelemetryProcessors.Count);
                Assert.IsType<QuickPulseTelemetryProcessor>(config.TelemetryProcessors[0]);
                Assert.IsType<FilteringTelemetryProcessor>(config.TelemetryProcessors[1]);
                Assert.IsType<AdaptiveSamplingTelemetryProcessor>(config.TelemetryProcessors[2]);

                Assert.Equal(samplingSettings.MaxTelemetryItemsPerSecond, ((AdaptiveSamplingTelemetryProcessor) config.TelemetryProcessors[2]).MaxTelemetryItemsPerSecond);
            }
        }

        [Fact]
        public void DependencyInjectionConfiguration_NoFilterConfiguresSampling()
        {
            var samplingSettings = new SamplingPercentageEstimatorSettings { MaxTelemetryItemsPerSecond = 1 };
            using (var host = new HostBuilder()
                .AddApplicationInsights("some key", samplingSettings)
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
        public void DependencyInjectionConfiguration_ConfiguresActive()
        {
            using (var host = new HostBuilder()
                    .AddApplicationInsights("some key", (c, l) => true, null)
                    .Build())
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
            }
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
