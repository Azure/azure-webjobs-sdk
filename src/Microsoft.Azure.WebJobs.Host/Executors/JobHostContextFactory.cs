// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Blobs;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal static class JobHostConfigurationExtensions
    {
        // Do full initialization (both static and runtime). 
        // This can be called multiple times on a config. 
        public static async Task<JobHostContext> CreateAndLogHostStartedAsync(
            this JobHostConfiguration config, 
            JobHost host, 
            CancellationToken shutdownToken, 
            CancellationToken cancellationToken)
        {
            var newServices = config.DoStaticInitialization();
            JobHostContext context = await newServices.DoRuntimeInitialization(config, host, shutdownToken, cancellationToken);

            return context;
        }

        // Static initialization. Returns a service provider with some new services initialized. 
        // The new services:
        // - can retrieve static config like binders and converters; but the listeners haven't yet started.
        // - can be flowed into the runtime initialization to get a JobHost spun up and running.
        // This shouldn't be async beccause we shouldn't need to do network calls for static init.        
        // This can be called multiple times on a config, which is why it returns a new ServiceProviderWrapper
        // instead of modifying the config.
        public static ServiceProviderWrapper DoStaticInitialization(this JobHostConfiguration config)
        {
            var ctx = new ServiceProviderWrapper(config);
            StaticInitWorker(ctx, config);        
            return ctx;
        }
                
        public static void StaticInitWorker(this ServiceProviderWrapper config, JobHostConfiguration rawConfig)
        {
            var consoleProvider = config.GetService<IConsoleProvider>();
            var nameResolver = config.GetService<INameResolver>();
            IWebJobsExceptionHandler exceptionHandler = config.GetService<IWebJobsExceptionHandler>();
            IQueueConfiguration queueConfiguration = config.GetService<IQueueConfiguration>();
            var blobsConfiguration = rawConfig.Blobs;

            IStorageAccountProvider storageAccountProvider = config.GetService<IStorageAccountProvider>();
            IBindingProvider bindingProvider = config.GetService<IBindingProvider>();
            SingletonManager singletonManager = config.GetService<SingletonManager>();

            IHostIdProvider hostIdProvider = config.GetService<IHostIdProvider>();
            var hostId = rawConfig.HostId;
            if (hostId != null)
            {
                hostIdProvider = new FixedHostIdProvider(hostId);
            }

            if (hostIdProvider == null)
            {
                // Need a deferred getter since the IFunctionIndexProvider service isn't created until later. 
                Func<IFunctionIndexProvider> deferredGetter = () => config.GetService<IFunctionIndexProvider>();
                hostIdProvider = new DynamicHostIdProvider(storageAccountProvider, deferredGetter);
            }
            config.AddService<IHostIdProvider>(hostIdProvider);

            AzureStorageDeploymentValidator.Validate();

            var converterManager = config.GetService<IConverterManager>();

            IExtensionTypeLocator extensionTypeLocator = config.GetService<IExtensionTypeLocator>();
            if (extensionTypeLocator == null)
            {
                extensionTypeLocator = new ExtensionTypeLocator(config.GetService<ITypeLocator>());
                config.AddService<IExtensionTypeLocator>(extensionTypeLocator);
            }

            ContextAccessor<IMessageEnqueuedWatcher> messageEnqueuedWatcherAccessor = new ContextAccessor<IMessageEnqueuedWatcher>();
            ContextAccessor<IBlobWrittenWatcher> blobWrittenWatcherAccessor = new ContextAccessor<IBlobWrittenWatcher>();
            ISharedContextProvider sharedContextProvider = new SharedContextProvider();

            // Create a wrapper TraceWriter that delegates to both the user 
            // TraceWriters specified via config (if present), as well as to Console
            TraceWriter trace = new ConsoleTraceWriter(rawConfig.Tracing, consoleProvider.Out);
            config.AddService<TraceWriter>(trace);

            ExtensionConfigContext context = new ExtensionConfigContext
            {
                Config = rawConfig,
                Trace = trace
            };
            InvokeExtensionConfigProviders(context);

            if (singletonManager == null)
            {
                singletonManager = new SingletonManager(storageAccountProvider, exceptionHandler, rawConfig.Singleton, trace, hostIdProvider, config.GetService<INameResolver>());
                config.AddService<SingletonManager>(singletonManager);
            }

            IExtensionRegistry extensions = config.GetExtensions();
            ITriggerBindingProvider triggerBindingProvider = DefaultTriggerBindingProvider.Create(nameResolver,
                storageAccountProvider, extensionTypeLocator, hostIdProvider, queueConfiguration, blobsConfiguration, exceptionHandler,
                messageEnqueuedWatcherAccessor, blobWrittenWatcherAccessor, sharedContextProvider, extensions, singletonManager, trace);
            config.AddService<ITriggerBindingProvider>(triggerBindingProvider);

            if (bindingProvider == null)
            {
                bindingProvider = DefaultBindingProvider.Create(nameResolver, converterManager, storageAccountProvider, extensionTypeLocator, messageEnqueuedWatcherAccessor, blobWrittenWatcherAccessor, extensions);
                config.AddService<IBindingProvider>(bindingProvider);
            }
        }

        // Do the runtime intitialization. This happens after the static initialization. 
        // This mainly means:
        // - indexing the functions 
        // - spinning up the listeners (so connecting to the services)
        private static async Task<JobHostContext> DoRuntimeInitialization(this ServiceProviderWrapper config, JobHostConfiguration rawConfig, JobHost host, CancellationToken shutdownToken, CancellationToken cancellationToken)
        {
            FunctionExecutor functionExecutor = config.GetService<FunctionExecutor>();
            IFunctionIndexProvider functionIndexProvider = config.GetService<IFunctionIndexProvider>();
            ITriggerBindingProvider triggerBindingProvider = config.GetService<ITriggerBindingProvider>();
            IBindingProvider bindingProvider = config.GetService<IBindingProvider>();
            SingletonManager singletonManager = config.GetService<SingletonManager>();
            IJobActivator activator = config.GetService<IJobActivator>();
            IHostIdProvider hostIdProvider = config.GetService<IHostIdProvider>();
            INameResolver nameResolver = config.GetService<INameResolver>();
            IExtensionRegistry extensions = config.GetExtensions();
            IStorageAccountProvider storageAccountProvider = config.GetService<IStorageAccountProvider>();

            IQueueConfiguration queueConfiguration = config.GetService<IQueueConfiguration>();
            var blobsConfiguration = rawConfig.Blobs;

            TraceWriter trace = config.GetService<TraceWriter>();
            IAsyncCollector<FunctionInstanceLogEntry> fastLogger = config.GetService<IAsyncCollector<FunctionInstanceLogEntry>>();                               
            IWebJobsExceptionHandler exceptionHandler = config.GetService<IWebJobsExceptionHandler>();

            if (exceptionHandler != null)
            {
                exceptionHandler.Initialize(host);
            }

            bool hasFastTableHook = config.GetService<IAsyncCollector<FunctionInstanceLogEntry>>() != null;
            bool noDashboardStorage = rawConfig.DashboardConnectionString == null;

            // Only testing will override these interfaces. 
            IHostInstanceLoggerProvider hostInstanceLoggerProvider = config.GetService<IHostInstanceLoggerProvider>();
            IFunctionInstanceLoggerProvider functionInstanceLoggerProvider = config.GetService<IFunctionInstanceLoggerProvider>();
            IFunctionOutputLoggerProvider functionOutputLoggerProvider = config.GetService<IFunctionOutputLoggerProvider>();

            if (hostInstanceLoggerProvider == null && functionInstanceLoggerProvider == null && functionOutputLoggerProvider == null)
            {
                if (hasFastTableHook && noDashboardStorage)
                {
                    var loggerProvider = new FastTableLoggerProvider(trace);
                    hostInstanceLoggerProvider = loggerProvider;
                    functionInstanceLoggerProvider = loggerProvider;
                    functionOutputLoggerProvider = loggerProvider;
                }
                else
                {
                    var loggerProvider = new DefaultLoggerProvider(storageAccountProvider, trace);
                    hostInstanceLoggerProvider = loggerProvider;
                    functionInstanceLoggerProvider = loggerProvider;
                    functionOutputLoggerProvider = loggerProvider;
                }
            }

            using (CancellationTokenSource combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, shutdownToken))
            {
                CancellationToken combinedCancellationToken = combinedCancellationSource.Token;

                await WriteSiteExtensionManifestAsync(combinedCancellationToken);

                IStorageAccount dashboardAccount = await storageAccountProvider.GetDashboardAccountAsync(combinedCancellationToken);

                IHostInstanceLogger hostInstanceLogger = await hostInstanceLoggerProvider.GetAsync(combinedCancellationToken);
                IFunctionInstanceLogger functionInstanceLogger = await functionInstanceLoggerProvider.GetAsync(combinedCancellationToken);                
                IFunctionOutputLogger functionOutputLogger = await functionOutputLoggerProvider.GetAsync(combinedCancellationToken);

                if (functionExecutor == null)
                {
                    functionExecutor = new FunctionExecutor(functionInstanceLogger, functionOutputLogger, exceptionHandler, trace, fastLogger);
                    config.AddService(functionExecutor);
                }

                if (functionIndexProvider == null)
                {
                    functionIndexProvider = new FunctionIndexProvider(config.GetService<ITypeLocator>(), triggerBindingProvider, bindingProvider, activator, functionExecutor, extensions, singletonManager, trace);

                    // Important to set this so that the func we passed to DynamicHostIdProvider can pick it up. 
                    config.AddService<IFunctionIndexProvider>(functionIndexProvider);
                }

                IFunctionIndex functions = await functionIndexProvider.GetAsync(combinedCancellationToken);
                IListenerFactory functionsListenerFactory = new HostListenerFactory(functions.ReadAll(), singletonManager, activator, nameResolver, trace);

                IFunctionExecutor hostCallExecutor;
                IListener listener;
                HostOutputMessage hostOutputMessage;

                string hostId = await hostIdProvider.GetHostIdAsync(cancellationToken);
                if (string.Compare(rawConfig.HostId, hostId, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    // if this isn't a static host ID, provide the HostId on the config
                    // so it is accessible
                    rawConfig.HostId = hostId;
                }

                if (dashboardAccount == null)
                {
                    hostCallExecutor = new ShutdownFunctionExecutor(shutdownToken, functionExecutor);

                    IListener factoryListener = new ListenerFactoryListener(functionsListenerFactory);
                    IListener shutdownListener = new ShutdownListener(shutdownToken, factoryListener);
                    listener = shutdownListener;

                    hostOutputMessage = new DataOnlyHostOutputMessage();
                }
                else
                {
                    string sharedQueueName = HostQueueNames.GetHostQueueName(hostId);
                    IStorageQueueClient dashboardQueueClient = dashboardAccount.CreateQueueClient();
                    IStorageQueue sharedQueue = dashboardQueueClient.GetQueueReference(sharedQueueName);
                    IListenerFactory sharedQueueListenerFactory = new HostMessageListenerFactory(sharedQueue,
                        queueConfiguration, exceptionHandler, trace, functions,
                        functionInstanceLogger, functionExecutor);

                    Guid hostInstanceId = Guid.NewGuid();
                    string instanceQueueName = HostQueueNames.GetHostQueueName(hostInstanceId.ToString("N"));
                    IStorageQueue instanceQueue = dashboardQueueClient.GetQueueReference(instanceQueueName);
                    IListenerFactory instanceQueueListenerFactory = new HostMessageListenerFactory(instanceQueue,
                        queueConfiguration, exceptionHandler, trace, functions,
                        functionInstanceLogger, functionExecutor);

                    HeartbeatDescriptor heartbeatDescriptor = new HeartbeatDescriptor
                    {
                        SharedContainerName = HostContainerNames.Hosts,
                        SharedDirectoryName = HostDirectoryNames.Heartbeats + "/" + hostId,
                        InstanceBlobName = hostInstanceId.ToString("N"),
                        ExpirationInSeconds = (int)HeartbeatIntervals.ExpirationInterval.TotalSeconds
                    };

                    IStorageBlockBlob blob = dashboardAccount.CreateBlobClient()
                        .GetContainerReference(heartbeatDescriptor.SharedContainerName)
                        .GetBlockBlobReference(heartbeatDescriptor.SharedDirectoryName + "/" + heartbeatDescriptor.InstanceBlobName);
                    IRecurrentCommand heartbeatCommand = new UpdateHostHeartbeatCommand(new HeartbeatCommand(blob));

                    IEnumerable<MethodInfo> indexedMethods = functions.ReadAllMethods();
                    Assembly hostAssembly = GetHostAssembly(indexedMethods);
                    string displayName = hostAssembly != null ? hostAssembly.GetName().Name : "Unknown";

                    hostOutputMessage = new DataOnlyHostOutputMessage
                    {
                        HostInstanceId = hostInstanceId,
                        HostDisplayName = displayName,
                        SharedQueueName = sharedQueueName,
                        InstanceQueueName = instanceQueueName,
                        Heartbeat = heartbeatDescriptor,
                        WebJobRunIdentifier = WebJobRunIdentifier.Current
                    };

                    hostCallExecutor = CreateHostCallExecutor(instanceQueueListenerFactory, heartbeatCommand,
                        exceptionHandler, shutdownToken, functionExecutor);
                    IListenerFactory hostListenerFactory = new CompositeListenerFactory(functionsListenerFactory,
                        sharedQueueListenerFactory, instanceQueueListenerFactory);
                    listener = CreateHostListener(hostListenerFactory, heartbeatCommand, exceptionHandler, shutdownToken);

                    // Publish this to Azure logging account so that a web dashboard can see it. 
                    await LogHostStartedAsync(functions, hostOutputMessage, hostInstanceLogger, combinedCancellationToken);
                }

                functionExecutor.HostOutputMessage = hostOutputMessage;

                IEnumerable<FunctionDescriptor> descriptors = functions.ReadAllDescriptors();
                int descriptorsCount = descriptors.Count();

                if (rawConfig.UsingDevelopmentSettings)
                {
                    trace.Verbose(string.Format("Development settings applied"));
                }

                if (descriptorsCount == 0)
                {
                    trace.Warning(string.Format("No job functions found. Try making your job classes and methods public. {0}",
                        Constants.ExtensionInitializationMessage), Host.TraceSource.Indexing);
                }
                else
                {
                    StringBuilder functionsTrace = new StringBuilder();
                    functionsTrace.AppendLine("Found the following functions:");

                    foreach (FunctionDescriptor descriptor in descriptors)
                    {
                        functionsTrace.AppendLine(descriptor.FullName);
                    }

                    trace.Info(functionsTrace.ToString(), Host.TraceSource.Indexing);
                }

                return new JobHostContext(functions, hostCallExecutor, listener, trace, fastLogger);
            }
        }

        private static void InvokeExtensionConfigProviders(ExtensionConfigContext context)
        {
            IExtensionRegistry extensions = context.Config.GetExtensions();

            IEnumerable<IExtensionConfigProvider> configProviders = extensions.GetExtensions(typeof(IExtensionConfigProvider)).Cast<IExtensionConfigProvider>();
            foreach (IExtensionConfigProvider configProvider in configProviders)
            {
                configProvider.Initialize(context);
            }
        }

        private static IFunctionExecutor CreateHostCallExecutor(IListenerFactory instanceQueueListenerFactory,
            IRecurrentCommand heartbeatCommand, IWebJobsExceptionHandler exceptionHandler,
            CancellationToken shutdownToken, IFunctionExecutor innerExecutor)
        {
            IFunctionExecutor heartbeatExecutor = new HeartbeatFunctionExecutor(heartbeatCommand,
                exceptionHandler, innerExecutor);
            IFunctionExecutor abortListenerExecutor = new AbortListenerFunctionExecutor(instanceQueueListenerFactory, heartbeatExecutor);
            IFunctionExecutor shutdownFunctionExecutor = new ShutdownFunctionExecutor(shutdownToken, abortListenerExecutor);
            return shutdownFunctionExecutor;
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private static IListener CreateHostListener(IListenerFactory allFunctionsListenerFactory,
            IRecurrentCommand heartbeatCommand, IWebJobsExceptionHandler exceptionHandler,
            CancellationToken shutdownToken)
        {
            IListener factoryListener = new ListenerFactoryListener(allFunctionsListenerFactory);
            IListener heartbeatListener = new HeartbeatListener(heartbeatCommand, exceptionHandler, factoryListener);
            IListener shutdownListener = new ShutdownListener(shutdownToken, heartbeatListener);
            return shutdownListener;
        }

        private static Assembly GetHostAssembly(IEnumerable<MethodInfo> methods)
        {
            // 1. Try to get the assembly name from the first method.
            MethodInfo firstMethod = methods.FirstOrDefault();

            if (firstMethod != null)
            {
                return firstMethod.DeclaringType.Assembly;
            }

            // 2. If there are no function definitions, try to use the entry assembly.
            Assembly entryAssembly = Assembly.GetEntryAssembly();

            if (entryAssembly != null)
            {
                return entryAssembly;
            }

            // 3. If there's no entry assembly either, we don't have anything to use.
            return null;
        }

        private static Task LogHostStartedAsync(IFunctionIndex functionIndex, HostOutputMessage hostOutputMessage,
            IHostInstanceLogger logger, CancellationToken cancellationToken)
        {
            IEnumerable<FunctionDescriptor> functions = functionIndex.ReadAllDescriptors();

            HostStartedMessage message = new HostStartedMessage
            {
                HostInstanceId = hostOutputMessage.HostInstanceId,
                HostDisplayName = hostOutputMessage.HostDisplayName,
                SharedQueueName = hostOutputMessage.SharedQueueName,
                InstanceQueueName = hostOutputMessage.InstanceQueueName,
                Heartbeat = hostOutputMessage.Heartbeat,
                WebJobRunIdentifier = hostOutputMessage.WebJobRunIdentifier,
                Functions = functions
            };

            return logger.LogHostStartedAsync(message, cancellationToken);
        }

        // When running in Azure Web Sites, write out a manifest file. This manifest file is read by
        // the Kudu site extension to provide custom behaviors for SDK jobs
        private static async Task WriteSiteExtensionManifestAsync(CancellationToken cancellationToken)
        {
            string jobDataPath = Environment.GetEnvironmentVariable(WebSitesKnownKeyNames.JobDataPath);
            if (jobDataPath == null)
            {
                // we're not in Azure Web Sites, bye bye.
                return;
            }

            const string Filename = "WebJobsSdk.marker";
            var path = Path.Combine(jobDataPath, Filename);
            const int DefaultBufferSize = 4096;

            try
            {
                using (Stream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, DefaultBufferSize, useAsync: true))
                using (TextWriter writer = new StreamWriter(stream))
                {
                    // content is not really important, this would help debugging though
                    cancellationToken.ThrowIfCancellationRequested();
                    await writer.WriteAsync(DateTime.UtcNow.ToString("s") + "Z");
                    await writer.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                if (ex is UnauthorizedAccessException || ex is IOException)
                {
                    // simultaneous access error or an error caused by some other issue
                    // ignore it and skip marker creation
                }
                else
                {
                    throw;
                }
            }
        }

        private class DataOnlyHostOutputMessage : HostOutputMessage
        {
            internal override void AddMetadata(IDictionary<string, string> metadata)
            {
                throw new NotSupportedException();
            }
        }
    }
}
