// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extensions for ApplicationInsights configurationon an <see cref="IHostBuilder"/>. 
    /// </summary>
    public static class ApplicationInsightsHostBuilderExtensions
    {
        /// <summary>
        /// Registers Application Insights and <see cref="ApplicationInsightsLoggerProvider"/> with an <see cref="IHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The host builder.</param>
        /// <param name="instrumentationKey">The Application Insights instrumentation key.</param>
        /// <param name="samplingSettings">The <see cref="SamplingPercentageEstimatorSettings"/> to use for configuring adaptive sampling. If null, sampling is disabled.</param>
        /// <returns>A <see cref="IHostBuilder"/> for chaining additional operations.</returns>
        public static IHostBuilder AddApplicationInsights(this IHostBuilder builder, string instrumentationKey, SamplingPercentageEstimatorSettings samplingSettings)
        {
            return AddApplicationInsights(builder, instrumentationKey, (_, level) => level > LogLevel.Debug, samplingSettings);
        }

        /// <summary>
        /// Registers Application Insights and <see cref="ApplicationInsightsLoggerProvider"/> with an <see cref="IHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The host builder.</param>
        /// <param name="instrumentationKey">The Application Insights instrumentation key.</param>
        /// <param name="filter">A filter that returns true if a message with the specified <see cref="LogLevel"/>
        /// and category should be logged. You can use <see cref="LogCategoryFilter.Filter(string, LogLevel)"/>
        /// or write a custom filter.</param>
        /// <param name="samplingSettings">The <see cref="SamplingPercentageEstimatorSettings"/> to use for configuring adaptive sampling. If null, sampling is disabled.</param>
        /// <returns>A <see cref="IHostBuilder"/> for chaining additional operations.</returns>
        public static IHostBuilder AddApplicationInsights(
            this IHostBuilder builder, 
            string instrumentationKey,
            Func<string, LogLevel, bool> filter,
            SamplingPercentageEstimatorSettings samplingSettings)
        {
            if (string.IsNullOrEmpty(instrumentationKey))
            {
                return builder;
            }

            builder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<ITelemetryInitializer, HttpDependenciesParsingTelemetryInitializer>();
                services.AddSingleton<ITelemetryInitializer, WebJobsRoleEnvironmentTelemetryInitializer>();
                services.AddSingleton<ITelemetryInitializer, WebJobsTelemetryInitializer>();
                services.AddSingleton<ITelemetryInitializer, WebJobsSanitizingInitializer>();
                services.AddSingleton<ITelemetryModule, QuickPulseTelemetryModule>();
                services.AddSingleton<ITelemetryModule, DependencyTrackingTelemetryModule>(provider =>
                {
                    var dependencyCollector = new DependencyTrackingTelemetryModule();
                    var excludedDomains = dependencyCollector.ExcludeComponentCorrelationHttpHeadersOnDomains;
                    excludedDomains.Add("core.windows.net");
                    excludedDomains.Add("core.chinacloudapi.cn");
                    excludedDomains.Add("core.cloudapi.de");
                    excludedDomains.Add("core.usgovcloudapi.net");
                    excludedDomains.Add("localhost");
                    excludedDomains.Add("127.0.0.1");

                    return dependencyCollector;
                });
                services.AddSingleton<ITelemetryModule, AppServicesHeartbeatTelemetryModule>();

                ServerTelemetryChannel serverChannel = new ServerTelemetryChannel();
                services.AddSingleton<ITelemetryChannel>(serverChannel);
                services.AddSingleton<TelemetryConfiguration>(provider =>
                {
                    // Because of https://github.com/Microsoft/ApplicationInsights-dotnet-server/issues/943
                    // we have to touch (and create) Active configuration before initializing telemetry modules 
                    TelemetryConfiguration activeConfig = TelemetryConfiguration.Active;

                    ITelemetryChannel channel = provider.GetService<ITelemetryChannel>();
                    TelemetryConfiguration config = TelemetryConfiguration.CreateDefault();
                    SetupTelemetryConfiguration(
                        config,
                        instrumentationKey, 
                        filter, 
                        samplingSettings,
                        channel,
                        provider.GetServices<ITelemetryInitializer>(),
                        provider.GetServices<ITelemetryModule>());

                    // Function users have no access to TelemetryConfiguration from host DI container,
                    // so we'll expect user to work with TelemetryConfiguration.Active
                    // Also, some ApplicationInsights internal operations (heartbeats) depend on
                    // the TelemetryConfiguration.Active being set so, we'll set up Active once per process lifetime.
                    if (string.IsNullOrEmpty(activeConfig.InstrumentationKey))
                    {
                        SetupTelemetryConfiguration(
                            activeConfig,
                            instrumentationKey,
                            filter,
                            samplingSettings,
                            new ServerTelemetryChannel(),
                            provider.GetServices<ITelemetryInitializer>(),
                            provider.GetServices<ITelemetryModule>());
                    }
                    return config;
                });
                services.AddSingleton<TelemetryClient>(provider =>
                {
                    TelemetryConfiguration configuration = provider.GetService<TelemetryConfiguration>();
                    TelemetryClient client = new TelemetryClient(configuration);

                    string assemblyVersion = GetAssemblyFileVersion(typeof(JobHost).Assembly);
                    client.Context.GetInternalContext().SdkVersion = $"webjobs: {assemblyVersion}";

                    return client;
                });

                services.AddSingleton<ILoggerProvider, ApplicationInsightsLoggerProvider>();
            });

            return builder;
        }

        internal static string GetAssemblyFileVersion(Assembly assembly)
        {
            AssemblyFileVersionAttribute fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            return fileVersionAttr?.Version ?? LoggingConstants.Unknown;
        }

        private static void SetupTelemetryConfiguration(
            TelemetryConfiguration configuration,
            string instrumentationKey,
            Func<string, LogLevel, bool> filter,
            SamplingPercentageEstimatorSettings samplingSettings,
            ITelemetryChannel channel,
            IEnumerable<ITelemetryInitializer> telemetryInitializers,
            IEnumerable<ITelemetryModule> telemetryModules)
        {
            if (instrumentationKey != null)
            {
                configuration.InstrumentationKey = instrumentationKey;
            }

            configuration.TelemetryChannel = channel;

            foreach (ITelemetryInitializer initializer in telemetryInitializers)
            {
                configuration.TelemetryInitializers.Add(initializer);
            }

            (channel as ServerTelemetryChannel)?.Initialize(configuration);

            QuickPulseTelemetryModule quickPulseModule = null;
            foreach (ITelemetryModule module in telemetryModules)
            {
                if (module is QuickPulseTelemetryModule telemetryModule)
                {
                    quickPulseModule = telemetryModule;
                }

                module.Initialize(configuration);
            }

            QuickPulseTelemetryProcessor quickPulseProcessor = null;
            configuration.TelemetryProcessorChainBuilder
                .Use((next) =>
                {
                    quickPulseProcessor = new QuickPulseTelemetryProcessor(next);
                    return quickPulseProcessor;
                })
                .Use((next) => new FilteringTelemetryProcessor(filter, next));

            if (samplingSettings != null)
            {
                configuration.TelemetryProcessorChainBuilder.Use((next) =>
                    new AdaptiveSamplingTelemetryProcessor(samplingSettings, null, next));
            }

            configuration.TelemetryProcessorChainBuilder.Build();
            quickPulseModule?.RegisterTelemetryProcessor(quickPulseProcessor);

            foreach (ITelemetryProcessor processor in configuration.TelemetryProcessors)
            {
                if (processor is ITelemetryModule module)
                {
                    module.Initialize(configuration);
                }
            }
        }
    }
}