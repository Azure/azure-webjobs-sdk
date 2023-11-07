// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    /// <summary>
    /// These tests help maintain our public surface area + dependencies. They will
    /// fail any time new dependencies or public surface area are added, ensuring
    /// we review such additions carefully.
    /// </summary>
    [Trait(TestTraits.CategoryTraitName, TestTraits.ScaleMonitoring)]
    public class PublicSurfaceTests
    {
        [Fact]
        public void WebJobs_Host_VerifyAssemblyReferences()
        {
            var names = TestHelpers.GetAssemblyReferences(typeof(JobHost).Assembly);

            foreach (var name in names)
            {
                if (name.StartsWith("Microsoft.WindowsAzure"))
                {
                    Assert.True(false, "Should not have azure dependency: " + name);
                }
            }
        }

        [Fact]
        public void WebJobs_Logging_VerifyPublicSurfaceArea()
        {
            var assembly = typeof(ILogWriter).Assembly;

            var expected = new[]
            {
                "FunctionId",
                "ActivationEvent",
                "FunctionInstanceLogItem",
                "FunctionInstanceStatus",
                "FunctionStatusExtensions",
                "FunctionVolumeTimelineEntry",
                "IAggregateEntry",
                "IFunctionDefinition",
                "IFunctionInstanceBaseEntry",
                "IFunctionInstanceBaseEntryExtensions",
                "ILogReader",
                "ILogWriter",
                "ILogTableProvider",
                "InstanceCountEntity",
                "IRecentFunctionEntry",
                "LogFactory",
                "ProjectionHelper",
                "RecentFunctionQuery",
                "Segment`1"
            };

            TestHelpers.AssertPublicTypes(expected, assembly);
        }

        [Fact]
        public void WebJobs_VerifyPublicSurfaceArea()
        {
            // The core WebJobs assembly should be truly minimal and have no extra dependencies. 
            var assembly = typeof(AutoResolveAttribute).Assembly;

            var expected = new[]
            {
                "AppSettingAttribute",
                "AutoResolveAttribute",
                "BinderExtensions",
                "BindingAttribute",
                "ConnectionProviderAttribute",
                "ConnectionStringAttribute",
                "DisableAttribute",
                "ExtensionAttribute",
                "FunctionNameAttribute",
                "IAsyncCollector`1",
                "IAttributeInvokeDescriptor`1",
                "IBinder",
                "ICollector`1",
                "IConnectionProvider",
                "NoAutomaticTriggerAttribute",
                "SingletonAttribute",
                "SingletonMode",
                "SingletonScope",
                "StorageAccountAttribute",
                "TimeoutAttribute",
                "ParameterBindingData"
            };

            TestHelpers.AssertPublicTypes(expected, assembly);
        }

        [Fact]
        public void WebJobs_Host_VerifyPublicSurfaceArea()
        {
            var assembly = typeof(Microsoft.Azure.WebJobs.JobHost).Assembly;

            var expected = new[]
            {
                "ApplyConversion`2",
                "AssemblyNameCache",
                "Binder",
                "BindingContext",
                "BindingDataProvider",
                "BindingFactory",
                "BindingProviderContext",
                "BindingTemplate",
                "BindingTemplateExtensions",
                "BindingTemplateSource",
                "BindStepOrder",
                "ConnectionStringNames",
                "DefaultExtensionRegistry",
                "DefaultExtensionRegistryFactory",
                "DefaultNameResolver",
                "DefaultWebJobsExceptionHandlerFactory",
                "DirectInvokeString",
                "ExceptionFormatter",
                "ExecutionContext",
                "ExecutionContextOptions",
                "ExecutionReason",
                "ExtensionConfigContext",
                "ExtensionInfo",
                "FluentBindingRule`1",
                "FluentBindingRule`1+FluentBinder",
                "FluentConverterRules`2",
                "FuncAsyncConverter",
                "FuncAsyncConverter`2",
                "FuncConverterBuilder",
                "FunctionBindingContext",
                "FunctionDescriptor",
                "FunctionException",
                "FunctionExceptionContext",
                "FunctionExceptionFilterAttribute",
                "FunctionExecutedContext",
                "FunctionExecutingContext",
                "FunctionFilterContext",
                "FunctionIndexingException",
                "FunctionInstanceExtensions",
                "FunctionInstanceFactoryContext",
                "FunctionInstanceLogEntry",
                "FunctionInvocationContext",
                "FunctionInvocationException",
                "FunctionInvocationFilterAttribute",
                "FunctionInvocationScope",
                "FunctionInvoker",
                "FunctionInvoker+Scope",
                "FunctionListenerException",
                "FunctionMetadata",
                "FunctionResult",
                "FunctionResultAggregatorOptions",
                "FunctionTimeoutException",
                "IArgumentBinding`1",
                "IArgumentBindingProvider`1",
                "IAsyncConverter`2",
                "IBinding",
                "IBindingDataProvider",
                "IBindingProvider",
                "IBindingSource",
                "IConverter`2",
                "IConverterManager",
                "IConverterManagerExtensions",
                "IDelayedException",
                "IDistributedLock",
                "IDistributedLockManager",
                "IEventCollectorFactory",
                "IEventCollectorProvider",
                "IExtensionConfigProvider",
                "IExtensionOptionsProvider",
                "IExtensionRegistry",
                "IExtensionRegistryExtensions",
                "IExtensionRegistryFactory",
                "IFunctionDefinition",
                "IFunctionExceptionFilter",
                "IFunctionExecutor",
                "IFunctionFilter",
                "IFunctionIndexLookup",
                "IFunctionInstance",
                "IFunctionInstanceEx",
                "IFunctionInstanceFactory",
                "IFunctionInvocationFilter",
                "IFunctionInvoker",
                "IHostIdProvider",
                "IHostSingletonManager",
                "IJobActivator",
                "IJobActivatorEx",
                "IJobHost",
                "IJobHostContextFactory",
                "IJobHostMetadataProvider",
                "IJobHostMetadataProviderFactory",
                "IListener",
                "IListenerFactory",
                "ILoadBalancerQueue",
                "INameResolver",
                "IOptionsFormatter",
                "IOptionsFormatter`1",
                "IOrderedValueBinder",
                "IResolutionPolicy",
                "ITriggerBinding",
                "ITriggerBindingProvider",
                "ITriggerBindingStrategy`2",
                "ITriggerData",
                "ITriggeredFunctionExecutor",
                "ITypeLocator",
                "IValueBinder",
                "IValueProvider",
                "IWatchable",
                "IWatcher",
                "IWebHookProvider",
                "IWebJobsConfigurationBuilder",
                "IWebJobsConfigurationStartup",
                "IWebJobsExceptionHandler",
                "IWebJobsExceptionHandlerFactory",
                "IWebJobsExtensionBuilder",
                "IWebJobsExtensionConfiguration`1",
                "IWebJobsStartup",
                "IWebJobsStartup2",
                "IWebJobsStartupTypeLocator",
                "JobHost",
                "JobHostContext",
                "JobHostFunctionTimeoutOptions",
                "JobHostOptions",
                "JobHostService",
                "ListenerFactoryContext",
                "LogCategories",
                "LogConstants",
                "LoggerExtensions",
                "NameResolverExtensions",
                "OpenType",
                "OpenTypeMatchContext",
                "ParameterDescriptor",
                "ParameterDisplayHints",
                "ParameterLog",
                "OpenType+Poco",
                "RecoverableException",
                "ScopeKeys",
                "SingletonOptions",
                "TraceEvent",
                "TraceWriter",
                "TriggerBindingProviderContext",
                "TriggerData",
                "TriggeredFunctionData",
                "TriggerParameterDescriptor",
                "ValueBindingContext",
                "IWebJobsBuilder",
                "WebJobsBuilderContext",
                "WebJobsBuilderExtensions",
                "WebJobsExceptionHandler",
                "WebJobsExtensionBuilderExtensions",
                "WebJobsHostBuilderExtensions",
                "WebJobsServiceCollectionExtensions",
                "WebJobsShutdownWatcher",
                "WebJobsStartupAttribute",
                "IConfigurationExtensions",
                "IScaleMonitor",
                "IScaleMonitor`1",
                "IScaleMonitorManager",
                "IScaleMonitorProvider",
                "ScaleMetrics",
                "ScaleStatus",
                "ScaleStatusContext",
                "ScaleStatusContext`1",
                "ScaleVote",
                "ScaleMonitorDescriptor",
                "IDrainModeManager",
                "IMethodInvoker`2",
                "MethodInvokerFactory",
                "IDrainModeManager",
                "IRetryStrategy",
                "RetryAttribute",
                "FixedDelayRetryAttribute",
                "ExponentialBackoffRetryAttribute",
                "RetryContext",
                "ConcurrencyManager",
                "ConcurrencyOptions",
                "ConcurrencyStatus",
                "HostConcurrencySnapshot",
                "ConcurrencyThrottleStatus",
                "ConcurrencyThrottleAggregateStatus",
                "FunctionConcurrencySnapshot",
                "HostHealthState",
                "HostProcessStatus",
                "IConcurrencyStatusRepository",
                "IConcurrencyThrottleManager",
                "IConcurrencyThrottleProvider",
                "IHostProcessMonitor",
                "IPrimaryHostStateProvider",
                "PrimaryHostCoordinatorOptions",
                "ThrottleState",
                "SharedListenerAttribute",
                "FunctionDataCacheKey",
                "ICacheAwareReadObject",
                "ICacheAwareWriteObject",
                "IFunctionDataCache",
                "SharedMemoryAttribute",
                "SharedMemoryMetadata",
                "FunctionActivityStatus",
                "IFunctionActivityStatusProvider",
                "SupportsRetryAttribute",
                "AppServicesHostingUtility",
                "ITargetScaler",
                "ITargetScalerManager",
                "ITargetScalerProvider",
                "TargetScalerDescriptor",
                "TargetScalerResult",
                "TargetScalerContext",
                "IScaleMetricsRepository",
                "IScaleStatusProvider",
                "ScaleOptions",
                "TriggerMetadata",
                "AggregateScaleStatus",
                "IListenerDecorator",
                "ListenerDecoratorContext"
            };

            TestHelpers.AssertPublicTypes(expected, assembly);
        }

        [Fact]
        public void WebJobs_Logging_ApplicationInsights_VerifyPublicSurfaceArea()
        {
            var assembly = typeof(ApplicationInsightsLogger).Assembly;

            var expected = new[]
            {
                "ApplicationInsightsLoggerOptions",
                "ApplicationInsightsDiagnosticConstants",
                "HttpAutoCollectionOptions",
                "ApplicationInsightsLoggerProvider",
                "ApplicationInsightsLoggingBuilderExtensions",
                "ISdkVersionProvider",
                "DependencyTrackingOptions",
                "TokenCredentialOptions"
            };

            TestHelpers.AssertPublicTypes(expected, assembly);
        }
    }
}
