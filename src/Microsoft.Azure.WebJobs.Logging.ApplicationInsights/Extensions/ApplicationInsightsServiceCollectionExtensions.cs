// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using Azure.Core;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
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
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    internal static class ApplicationInsightsServiceCollectionExtensions
    {

        public static IServiceCollection AddApplicationInsights(this IServiceCollection services)
        {
            return services.AddApplicationInsights(_ => { }, _ => { });
        }


        public static IServiceCollection AddApplicationInsights(this IServiceCollection services,
            Action<ApplicationInsightsLoggerOptions> loggerOptionsConfiguration)
        {
            services.AddApplicationInsights(loggerOptionsConfiguration, _ => { });
            return services;
        }

        internal static IServiceCollection AddApplicationInsights(this IServiceCollection services,
            Action<ApplicationInsightsLoggerOptions> loggerOptionsConfiguration,
            Action<TelemetryConfiguration> additionalTelemetryConfig)
        {
            services.TryAddSingleton<ISdkVersionProvider, WebJobsSdkVersionProvider>();
            services.TryAddSingleton<IRoleInstanceProvider, WebJobsRoleInstanceProvider>();

            // Bind to the configuration section registered with 
            services.AddOptions<ApplicationInsightsLoggerOptions>()
                .Configure<ILoggerProviderConfiguration<ApplicationInsightsLoggerProvider>>((options, config) =>
                {
                    config.Configuration?.Bind(options);
                });

            services.AddSingleton<ITelemetryInitializer, HttpDependenciesParsingTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer>(provider =>
            {
                ApplicationInsightsLoggerOptions options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;
                if (options.HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection)
                {
                    var httpContextAccessor = provider.GetService<IHttpContextAccessor>();
                    if (httpContextAccessor != null)
                    {
                        return new ClientIpHeaderTelemetryInitializer(httpContextAccessor);
                    }
                }

                return NullTelemetryInitializer.Instance;
            });

            services.AddSingleton<ITelemetryInitializer, WebJobsRoleEnvironmentTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, WebJobsTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, MetricSdkVersionTelemetryInitializer>();
            services.AddSingleton<QuickPulseInitializationScheduler>();
            services.AddSingleton<QuickPulseTelemetryModule>();

            services.AddSingleton<ITelemetryModule>(provider =>
            {
                ApplicationInsightsLoggerOptions options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;
                if (options.EnableLiveMetrics)
                {
                    return provider.GetService<QuickPulseTelemetryModule>();
                }

                return NullTelemetryModule.Instance;
            });

            services.AddSingleton<ITelemetryModule>(provider =>
            {
                ApplicationInsightsLoggerOptions options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;
                if (options.EnablePerformanceCountersCollection)
                {
                    return new PerformanceCollectorModule
                    {
                        // Disabling this can improve cold start times
                        EnableIISExpressPerformanceCounters = false
                    };
                }

                return NullTelemetryModule.Instance;
            });

            services.AddSingleton<ITelemetryModule>(provider =>
            {
                ApplicationInsightsLoggerOptions options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;
                if (options.DiagnosticsEventListenerLogLevel != null)
                {
                    return new SelfDiagnosticsTelemetryModule((EventLevel)options.DiagnosticsEventListenerLogLevel);
                }

                return NullTelemetryModule.Instance;
            });

            services.AddSingleton<IApplicationIdProvider, ApplicationInsightsApplicationIdProvider>();

            services.AddSingleton<ITelemetryModule>(provider =>
            {
                var options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;

                DependencyTrackingTelemetryModule dependencyCollector = null;
                if (options.EnableDependencyTracking)
                {
                    dependencyCollector = new DependencyTrackingTelemetryModule();
                    var excludedDomains = dependencyCollector.ExcludeComponentCorrelationHttpHeadersOnDomains;
                    excludedDomains.Add("core.windows.net");
                    excludedDomains.Add("core.chinacloudapi.cn");
                    excludedDomains.Add("core.cloudapi.de");
                    excludedDomains.Add("core.usgovcloudapi.net");
                    excludedDomains.Add("localhost");
                    excludedDomains.Add("127.0.0.1");

                    var includedActivities = dependencyCollector.IncludeDiagnosticSourceActivities;
                    includedActivities.Add("Microsoft.Azure.ServiceBus");
                    includedActivities.Add("Microsoft.Azure.EventHubs");

                    if (options.DependencyTrackingOptions != null)
                    {
                        dependencyCollector.DisableRuntimeInstrumentation = options.DependencyTrackingOptions.DisableRuntimeInstrumentation;
                        dependencyCollector.DisableDiagnosticSourceInstrumentation = options.DependencyTrackingOptions.DisableDiagnosticSourceInstrumentation;
                        dependencyCollector.EnableLegacyCorrelationHeadersInjection = options.DependencyTrackingOptions.EnableLegacyCorrelationHeadersInjection;
                        dependencyCollector.EnableRequestIdHeaderInjectionInW3CMode = options.DependencyTrackingOptions.EnableRequestIdHeaderInjectionInW3CMode;
                        dependencyCollector.EnableSqlCommandTextInstrumentation = options.DependencyTrackingOptions.EnableSqlCommandTextInstrumentation;
                        dependencyCollector.SetComponentCorrelationHttpHeaders = options.DependencyTrackingOptions.SetComponentCorrelationHttpHeaders;
                        dependencyCollector.EnableAzureSdkTelemetryListener = options.DependencyTrackingOptions.EnableAzureSdkTelemetryListener;
                    }

                    return dependencyCollector;
                }

                return NullTelemetryModule.Instance;
            });

            services.AddSingleton<ITelemetryModule>(provider =>
            {
                var options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;
                if (options.HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection)
                {
                    var appIdProvider = provider.GetService<IApplicationIdProvider>();

                    return new RequestTrackingTelemetryModule(appIdProvider)
                    {
                        CollectionOptions = new RequestCollectionOptions
                        {
                            TrackExceptions = false, // webjobs/functions track exceptions themselves
                            InjectResponseHeaders = options.HttpAutoCollectionOptions.EnableResponseHeaderInjection
                        }
                    };
                }

                return NullTelemetryModule.Instance;
            });

            services.AddSingleton<ITelemetryModule, AppServicesHeartbeatTelemetryModule>();

            services.AddSingleton<ITelemetryChannel, ServerTelemetryChannel>();
            services.AddSingleton<TelemetryConfiguration>(provider =>
            {
                ApplicationInsightsLoggerOptions options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;

                Activity.DefaultIdFormat = options.HttpAutoCollectionOptions.EnableW3CDistributedTracing
                    ? ActivityIdFormat.W3C
                    : ActivityIdFormat.Hierarchical;
                Activity.ForceDefaultIdFormat = true;

                // If we do not want to filter LiveMetrics logs, we need to "late filter" using the 
                // custom filter options that were passed in during initialization.
                LoggerFilterOptions filterOptions = null;
                if (options.EnableLiveMetrics && !options.EnableLiveMetricsFilters)
                {
                    filterOptions = CreateFilterOptions(provider.GetService<IOptions<LoggerFilterOptions>>().Value);
                }

                ITelemetryChannel channel = provider.GetService<ITelemetryChannel>();
                TelemetryConfiguration config = TelemetryConfiguration.CreateDefault();

                IApplicationIdProvider appIdProvider = provider.GetService<IApplicationIdProvider>();
                ISdkVersionProvider sdkVersionProvider = provider.GetService<ISdkVersionProvider>();
                IRoleInstanceProvider roleInstanceProvider = provider.GetService<IRoleInstanceProvider>();

                // Because of https://github.com/Microsoft/ApplicationInsights-dotnet-server/issues/943
                // we have to touch (and create) Active configuration before initializing telemetry modules
                // Active configuration is used to report AppInsights heartbeats
                // role environment telemetry initializer is needed to correlate heartbeats to particular host

                var activeConfig = TelemetryConfiguration.Active;
                if (!string.IsNullOrEmpty(options.InstrumentationKey) &&
                    string.IsNullOrEmpty(activeConfig.InstrumentationKey))
                {
                    activeConfig.InstrumentationKey = options.InstrumentationKey;
                }

                // Set ConnectionString second because it takes precedence and
                // we don't want InstrumentationKey to overwrite the value
                // ConnectionString sets
                if (!string.IsNullOrEmpty(options.ConnectionString) &&
                    string.IsNullOrEmpty(activeConfig.ConnectionString))
                {
                    activeConfig.ConnectionString = options.ConnectionString;
                }

                if (!activeConfig.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>().Any())
                {
                    activeConfig.TelemetryInitializers.Add(new WebJobsRoleEnvironmentTelemetryInitializer());
                    activeConfig.TelemetryInitializers.Add(new WebJobsTelemetryInitializer(sdkVersionProvider, roleInstanceProvider, provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>()));
                }

                SetupTelemetryConfiguration(
                    config,
                    options,
                    channel,
                    provider.GetServices<ITelemetryInitializer>(),
                    provider.GetServices<ITelemetryModule>(),
                    appIdProvider,
                    filterOptions,
                    roleInstanceProvider,
                    provider.GetService<QuickPulseInitializationScheduler>(),
                    additionalTelemetryConfig);

                return config;
            });

            services.AddSingleton<TelemetryClient>(provider =>
            {
                TelemetryConfiguration configuration = provider.GetService<TelemetryConfiguration>();
                TelemetryClient client = new TelemetryClient(configuration);

                ISdkVersionProvider versionProvider = provider.GetService<ISdkVersionProvider>();
                client.Context.GetInternalContext().SdkVersion = versionProvider?.GetSdkVersion();

                return client;
            });

            services.AddSingleton<ILoggerProvider, ApplicationInsightsLoggerProvider>();

            if (loggerOptionsConfiguration != null)
            {
                services.Configure<ApplicationInsightsLoggerOptions>(loggerOptionsConfiguration);
            }

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
            LoggerFilterOptions filterOptions,
            IRoleInstanceProvider roleInstanceProvider,
            QuickPulseInitializationScheduler delayer,
            Action<TelemetryConfiguration> additionalTelemetryConfig)
        {
            if (options.ConnectionString != null)
            {
                configuration.ConnectionString = options.ConnectionString;
            }
            else if (options.InstrumentationKey != null)
            {
                configuration.InstrumentationKey = options.InstrumentationKey;
            }

            // Default is connection string based ingestion
            if (options.TokenCredentialOptions?.CreateTokenCredential() is TokenCredential credential)
            {
                configuration.SetAzureTokenCredential(credential);
            }

            configuration.TelemetryChannel = channel;

            foreach (ITelemetryInitializer initializer in telemetryInitializers)
            {
                if (!(initializer is NullTelemetryInitializer))
                {
                    configuration.TelemetryInitializers.Add(initializer);
                }
            }

            (channel as ServerTelemetryChannel)?.Initialize(configuration);

            QuickPulseTelemetryModule quickPulseModule = null;
            foreach (ITelemetryModule module in telemetryModules)
            {
                if (module is QuickPulseTelemetryModule telemetryModule)
                {
                    quickPulseModule = telemetryModule;
                    if (options.LiveMetricsAuthenticationApiKey != null)
                    {
                        quickPulseModule.AuthenticationApiKey = options.LiveMetricsAuthenticationApiKey;
                    }

                    quickPulseModule.ServerId = roleInstanceProvider?.GetRoleInstanceName();

                    // QuickPulse can have a startup performance hit, so delay its initialization.
                    delayer.ScheduleInitialization(() => module.Initialize(configuration), options.LiveMetricsInitializationDelay);
                }
                else if (module != null)
                {
                    module.Initialize(configuration);
                }
            }

            // Metrics extractor must be added before filtering and adaptive sampling telemetry processor to account for all the data.
            if (options.EnableAutocollectedMetricsExtractor)
            {
                configuration.TelemetryProcessorChainBuilder
                    .Use((next) => new AutocollectedMetricsExtractor(next));
            }

            QuickPulseTelemetryProcessor quickPulseProcessor = null;
            configuration.TelemetryProcessorChainBuilder
                .Use((next) => new OperationFilteringTelemetryProcessor(next));

            if (options.EnableLiveMetrics)
            {
                configuration.TelemetryProcessorChainBuilder.Use((next) =>
                {
                    quickPulseProcessor = new QuickPulseTelemetryProcessor(next);
                    return quickPulseProcessor;
                });
            }

            // No need to "late filter" as the logs will already be filtered before they are sent to the Logger.
            if (filterOptions != null)
            {
                configuration.TelemetryProcessorChainBuilder.Use((next) => new FilteringTelemetryProcessor(filterOptions, next));
            }

            if (options.SamplingSettings != null)
            {
                configuration.TelemetryProcessorChainBuilder.Use((next) =>
                {
                    var processor = new AdaptiveSamplingTelemetryProcessor(options.SamplingSettings, null, next);
                    if (options.SamplingExcludedTypes != null)
                    {
                        processor.ExcludedTypes = options.SamplingExcludedTypes;
                    }
                    if (options.SamplingIncludedTypes != null)
                    {
                        processor.IncludedTypes = options.SamplingIncludedTypes;
                    }
                    return processor;
                });
            }

            additionalTelemetryConfig?.Invoke(configuration);

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
    }
}