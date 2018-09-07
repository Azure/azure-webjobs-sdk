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
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    internal static class ApplicationInsightsServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationInsights(this IServiceCollection services, Action<ApplicationInsightsLoggerOptions> configure)
        {
            services.AddApplicationInsights();
            if (configure != null)
            {
                services.Configure<ApplicationInsightsLoggerOptions>(configure);
            }
            return services;
        }

        public static IServiceCollection AddApplicationInsights(this IServiceCollection services)
        {
            // Bind to the configuration section registered with 
            services.AddOptions<ApplicationInsightsLoggerOptions>()
                .Configure<ILoggerProviderConfiguration<ApplicationInsightsLoggerProvider>>((options, config) =>
                {
                    config.Configuration?.Bind(options);
                });

            services.AddSingleton<ITelemetryInitializer, HttpDependenciesParsingTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, WebJobsRoleEnvironmentTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, WebJobsTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, WebJobsSanitizingInitializer>();
            services.AddSingleton<ITelemetryModule, QuickPulseTelemetryModule>();

            services.AddSingleton<IApplicationIdProvider, ApplicationInsightsApplicationIdProvider>();

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

                var includedActivities = dependencyCollector.IncludeDiagnosticSourceActivities;
                includedActivities.Add("Microsoft.Azure.ServiceBus");

                return dependencyCollector;
            });
            services.AddSingleton<ITelemetryModule, AppServicesHeartbeatTelemetryModule>();

            services.AddSingleton<ITelemetryChannel, ServerTelemetryChannel>();
            services.AddSingleton<TelemetryConfiguration>(provider =>
            {
                ApplicationInsightsLoggerOptions options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;
                LoggerFilterOptions filterOptions = CreateFilterOptions(provider.GetService<IOptions<LoggerFilterOptions>>().Value);

                ITelemetryChannel channel = provider.GetService<ITelemetryChannel>();
                TelemetryConfiguration config = TelemetryConfiguration.CreateDefault();

                IApplicationIdProvider appIdProvider = provider.GetService<IApplicationIdProvider>();

                SetupTelemetryConfiguration(
                    config,
                    options,
                    channel,
                    provider.GetServices<ITelemetryInitializer>(),
                    provider.GetServices<ITelemetryModule>(),
                    appIdProvider,
                    filterOptions);

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

            return services;
        }

        internal static LoggerFilterOptions CreateFilterOptions(LoggerFilterOptions registeredOptions)
        {
            // We want our own copy of the rules, excluding the 'allow-all' rule that we added for this provider.
            LoggerFilterOptions customFilterOptions = new LoggerFilterOptions
            {
                MinLevel = registeredOptions.MinLevel
            };

            ApplicationInsightsLoggerFilterRule allowAllRule = registeredOptions.Rules.OfType<ApplicationInsightsLoggerFilterRule>().Single();

            // Copy all existing rules
            foreach (LoggerFilterRule rule in registeredOptions.Rules)
            {
                if (rule != allowAllRule)
                {
                    customFilterOptions.Rules.Add(rule);
                }
            }

            // Copy 'hidden' rules
            foreach (LoggerFilterRule rule in allowAllRule.ChildRules)
            {
                customFilterOptions.Rules.Add(rule);
            }

            return customFilterOptions;
        }

        private static void SetupTelemetryConfiguration(
            TelemetryConfiguration configuration,
            ApplicationInsightsLoggerOptions options,
            ITelemetryChannel channel,
            IEnumerable<ITelemetryInitializer> telemetryInitializers,
            IEnumerable<ITelemetryModule> telemetryModules,
            IApplicationIdProvider applicationIdProvider,
            LoggerFilterOptions filterOptions)
        {
            if (options.InstrumentationKey != null)
            {
                configuration.InstrumentationKey = options.InstrumentationKey;

                // Because of https://github.com/Microsoft/ApplicationInsights-dotnet-server/issues/943
                // we have to touch (and create) Active configuration before initializing telemetry modules 
                TelemetryConfiguration.Active.InstrumentationKey = options.InstrumentationKey;
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
                    if (options.QuickPulseAuthenticationApiKey != null)
                    {
                        quickPulseModule.AuthenticationApiKey = options.QuickPulseAuthenticationApiKey;
                    }
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
                .Use((next) => new FilteringTelemetryProcessor(filterOptions, next));

            if (options.SamplingSettings != null)
            {
                configuration.TelemetryProcessorChainBuilder.Use((next) =>
                    new AdaptiveSamplingTelemetryProcessor(options.SamplingSettings, null, next));
            }

            if (options.SnapshotConfiguration != null)
            {
                configuration.TelemetryProcessorChainBuilder.UseSnapshotCollector(options.SnapshotConfiguration);
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

            configuration.ApplicationIdProvider = applicationIdProvider;
        }
        internal static string GetAssemblyFileVersion(Assembly assembly)
        {
            AssemblyFileVersionAttribute fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            return fileVersionAttr?.Version ?? LoggingConstants.Unknown;
        }
    }
}