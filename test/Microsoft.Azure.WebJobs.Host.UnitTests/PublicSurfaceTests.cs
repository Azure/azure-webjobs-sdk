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
                "TimeoutAttribute"
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
                "FluentBinder",
                "FluentBindingRule`1",
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
                "FunctionInstanceFactoryContext",
                "FunctionInstanceLogEntry",
                "FunctionInvocationContext",
                "FunctionInvocationException",
                "FunctionInvocationFilterAttribute",
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
                "IExtensionRegistry",
                "IExtensionRegistryExtensions",
                "IExtensionRegistryFactory",
                "IFunctionDefinition",
                "IFunctionExceptionFilter",
                "IFunctionExecutor",
                "IFunctionFilter",
                "IFunctionIndexLookup",
                "IFunctionInstance",
                "IFunctionInstanceFactory",
                "IFunctionInvocationFilter",
                "IFunctionInvoker",
                "IHostIdProvider",
                "IHostSingletonManager",
                "IJobActivator",
                "IJobHost",
                "IJobHostContextFactory",
                "IJobHostMetadataProvider",
                "IJobHostMetadataProviderFactory",
                "IListener",
                "IListenerFactory",
                "IQueueFactory",
                "INameResolver",
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
                "IWebJobsExceptionHandler",
                "IWebJobsExceptionHandlerFactory",
                "IWebJobsExtensionBuilder",
                "IWebJobsExtensionConfiguration`1",
                "IWebJobsStartup",
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
                "Poco",
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
                "WebJobsBuilderExtensions",
                "WebJobsExceptionHandler",
                "WebJobsExtensionBuilderExtensions",
                "WebJobsHostBuilderExtensions",
                "WebJobsServiceCollectionExtensions",
                "WebJobsShutdownWatcher",
                "WebJobsStartupAttribute",
                "IConfigurationExtensions"
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
                "ApplicationInsightsLoggerProvider",
                "ApplicationInsightsLoggingBuilderExtensions"
            };

            TestHelpers.AssertPublicTypes(expected, assembly);
        }
    }
}
