// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public void AssemblyReferences_InJobsAssembly()
        {
            // The DLL containing the binding attributes should be truly minimal and have no extra dependencies. 
            var names = GetAssemblyReferences(typeof(QueueTriggerAttribute).Assembly)
                .OrderBy(n => n);

            var expectedReferences = new string[]
            {
                "Microsoft.Azure.WebJobs",
                "Microsoft.Azure.WebJobs.Host",
                "Microsoft.Extensions.DependencyInjection.Abstractions",
                "Microsoft.Extensions.Hosting.Abstractions",
                "Microsoft.Extensions.Logging.Abstractions",
                "Microsoft.Extensions.Options",
                "Microsoft.WindowsAzure.Storage",
                "netstandard",
                "Newtonsoft.Json",
                "System.ComponentModel.Annotations",
            }.OrderBy(n => n);

            Assert.True(expectedReferences.SequenceEqual(names, StringComparer.Ordinal), 
                "Assembly references do not match the expected references");
        }

        [Fact]
        public void AssemblyReferences_InJobsHostAssembly()
        {
            var names = GetAssemblyReferences(typeof(JobHost).Assembly);

            foreach (var name in names)
            {
                if (name.StartsWith("Microsoft.WindowsAzure"))
                {
                    // Only azure dependency is on the storage sdk
                    Assert.Equal("Microsoft.WindowsAzure.Storage", name);
                }
            }
        }

        [Fact]
        public void LoggingPublicSurface_LimitedToSpecificTypes()
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

            AssertPublicTypes(expected, assembly);
        }

        [Fact]
        public void ServiceBusPublicSurface_LimitedToSpecificTypes()
        {
            var assembly = typeof(ServiceBusAttribute).Assembly;

            var expected = new[]
            {
                "EntityType",
                "MessageProcessor",
                "MessagingProvider",
                "ServiceBusAccountAttribute",
                "ServiceBusAttribute",
                "ServiceBusTriggerAttribute",
                "ServiceBusExtensionConfig",
                "ServiceBusHostBuilderExtensions",
                "ServiceBusOptions"
            };

            AssertPublicTypes(expected, assembly);
        }

        [Fact]
        public void WebJobsPublicSurface_LimitedToSpecificTypes()
        {
            var assembly = typeof(QueueTriggerAttribute).Assembly;

            var expected = new[]
            {
               "BlobAttribute",
                "BlobNameValidationAttribute",
                "BlobParameterDescriptor",
                "BlobTriggerAttribute",
                "BlobTriggerParameterDescriptor",
                "IQueueProcessorFactory",
                "JobHostBlobsOptions",
                "JobHostQueuesOptions",
                "PoisonMessageEventArgs",
                "QueueAttribute",
                "QueueParameterDescriptor",
                "QueueProcessor",
                "QueueProcessorFactoryContext",
                "QueueTriggerAttribute",
                "QueueTriggerParameterDescriptor",
                "StorageHostBuilderExtensions",
                "TableAttribute",
                "TableEntityParameterDescriptor",
                "TableExtension",
                "TableParameterDescriptor",
                "XStorageAccount",
                "StorageAccountProvider",
            };

            AssertPublicTypes(expected, assembly);
        }

        [Fact]
        public void WebJobsHostPublicSurface_LimitedToSpecificTypes()
        {
            var assembly = typeof(Microsoft.Azure.WebJobs.JobHost).Assembly;

            var expected = new[]
            {
                "AmbientConnectionStringProvider",
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
                "ExecutionReason",
                "ExtensionConfigContext",
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
                "IConnectionStringProvider",
                "IConverter`2",
                "IConverterManager",
                "IConverterManagerExtensions",
                "IDelayedException",
                "IDispatchQueueHandler",
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
                "ILoadbalancerQueue",
                "IMessageHandler",
                "INameResolver",
                "InMemoryLoadbalancerQueue",
                "InMemorySingletonManager",
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
                "IWebJobsStartup",
                "IWebJobsStartupTypeDiscoverer",
                "JobHost",
                "JobHostBuilder",
                "JobHostContext",
                "JobHostFunctionTimeoutOptions",
                "JobHostOptions",
                "JobHostService",
                "ListenerFactoryContext",
                "LogCategories",
                "LogCategoryFilter",
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
                "WebJobsExceptionHandler",
                "WebJobsHostExtensions",
                "WebJobsServiceCollectionExtensions",
                "WebJobsShutdownWatcher",
                "WebJobsStartupAttribute",
            };

            AssertPublicTypes(expected, assembly);
        }

        [Fact]
        public void ApplicationInsightsPublicSurface_LimitedToSpecificTypes()
        {
            var assembly = typeof(ApplicationInsightsLogger).Assembly;

            var expected = new[]
            {
                "ApplicationInsightsLoggerProvider",
                "ApplicationInsightsHostBuilderExtensions"
            };

            AssertPublicTypes(expected, assembly);
        }

        private static List<string> GetAssemblyReferences(Assembly assembly)
        {
            var assemblyRefs = assembly.GetReferencedAssemblies();
            var names = (from assemblyRef in assemblyRefs
                         orderby assemblyRef.Name.ToLowerInvariant()
                         select assemblyRef.Name).ToList();
            return names;
        }

        private static void AssertPublicTypes(IEnumerable<string> expected, Assembly assembly)
        {
            var publicTypes = (assembly.GetExportedTypes()
                .Select(type => type.Name)
                .OrderBy(n => n));

            AssertPublicTypes(expected.ToArray(), publicTypes.ToArray());
        }

        private static void AssertPublicTypes(string[] expected, string[] actual)
        {
            var newlyIntroducedPublicTypes = actual.Except(expected).ToArray();

            if (newlyIntroducedPublicTypes.Length > 0)
            {
                string message = String.Format("Found {0} unexpected public type{1}: \r\n{2}",
                    newlyIntroducedPublicTypes.Length,
                    newlyIntroducedPublicTypes.Length == 1 ? "" : "s",
                    string.Join("\r\n", newlyIntroducedPublicTypes));
                Assert.True(false, message);
            }

            var missingPublicTypes = expected.Except(actual).ToArray();

            if (missingPublicTypes.Length > 0)
            {
                string message = String.Format("missing {0} public type{1}: \r\n{2}",
                    missingPublicTypes.Length,
                    missingPublicTypes.Length == 1 ? "" : "s",
                    string.Join("\r\n", missingPublicTypes));
                Assert.True(false, message);
            }
        }
    }
}
