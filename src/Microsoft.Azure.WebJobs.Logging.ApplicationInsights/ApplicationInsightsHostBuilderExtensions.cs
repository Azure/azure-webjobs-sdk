// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel.Implementation;
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
                services.AddSingleton<ITelemetryInitializer, WebJobsRoleEnvironmentTelemetryInitializer>();
                services.AddSingleton<ITelemetryInitializer, WebJobsTelemetryInitializer>();
                services.AddSingleton<ITelemetryInitializer, WebJobsSanitizingInitializer>();
                services.AddSingleton<ITelemetryModule, QuickPulseTelemetryModule>();

                ServerTelemetryChannel serverChannel = new ServerTelemetryChannel();
                services.AddSingleton<ITelemetryModule>(serverChannel);
                services.AddSingleton<ITelemetryChannel>(serverChannel);
                services.AddSingleton<TelemetryConfiguration>(provider =>
                {
                    ITelemetryChannel channel = provider.GetService<ITelemetryChannel>();
                    TelemetryConfiguration config = new TelemetryConfiguration(instrumentationKey, channel);
                    
                    foreach (ITelemetryInitializer initializer in provider.GetServices<ITelemetryInitializer>())
                    {
                        config.TelemetryInitializers.Add(initializer);
                    }

                    QuickPulseTelemetryModule quickPulseModule = null;
                    foreach (ITelemetryModule module in provider.GetServices<ITelemetryModule>())
                    {
                        if (module is QuickPulseTelemetryModule telemetryModule)
                        {
                            quickPulseModule = telemetryModule;
                        }
                        module.Initialize(config);
                    }

                    QuickPulseTelemetryProcessor processor = null;
                    config.TelemetryProcessorChainBuilder
                        .Use((next) =>
                        {
                            processor = new QuickPulseTelemetryProcessor(next);
                            return processor;
                        })
                        .Use((next) => new FilteringTelemetryProcessor(filter, next));

                    if (samplingSettings != null)
                    {
                        config.TelemetryProcessorChainBuilder.Use((next) =>
                            new AdaptiveSamplingTelemetryProcessor(samplingSettings, null, next));
                    }

                    config.TelemetryProcessorChainBuilder.Build();
                    quickPulseModule?.RegisterTelemetryProcessor(processor);

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
    }
}