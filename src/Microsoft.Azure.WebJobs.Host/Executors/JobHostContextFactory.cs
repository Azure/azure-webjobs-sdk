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
using Microsoft.Azure.WebJobs.Host.Blobs.Bindings;
using Microsoft.Azure.WebJobs.Host.Blobs.Triggers;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Dispatch;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Queues.Bindings;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.Azure.WebJobs.Host.Tables;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;


namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class JobHostContextFactory : IJobHostContextFactory
    {
        private readonly IFunctionExecutor _functionExecutor;
        private readonly IFunctionIndexProvider _functionIndexProvider;
        private readonly ITriggerBindingProvider _triggerBindingProvider;
        private readonly SingletonManager _singletonManager;
        private readonly IJobActivator _activator;
        private readonly IHostIdProvider _hostIdProvider;
        private readonly INameResolver _nameResolver;
        private readonly IExtensionRegistry _extensions;
        private readonly IStorageAccountProvider _storageAccountProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IFunctionResultAggregatorFactory _aggregatorFactory;
        private readonly IOptions<JobHostQueuesOptions> _queueConfiguration;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly SharedQueueHandler _sharedQueueHandler;
        private readonly IOptions<JobHostOptions> _jobHostOptions;
        private readonly IOptions<FunctionResultAggregatorOptions> _aggregatorOptions;
        private readonly IOptions<JobHostBlobsOptions> _blobsConfiguration;
        private readonly IServiceProvider _serviceProvider;

        public JobHostContextFactory(IFunctionExecutor functionExecutor,
            IFunctionIndexProvider functionIndexProvider,
            ITriggerBindingProvider triggerBindingProvider,
            SingletonManager singletonManager,
            IJobActivator activator,
            IHostIdProvider hostIdProvider,
            INameResolver nameResolver,
            IExtensionRegistry extensions,
            IStorageAccountProvider storageAccountProvider,
            ILoggerFactory loggerFactory,
            IFunctionResultAggregatorFactory aggregatorFactory,
            IWebJobsExceptionHandler exceptionHandler,
            SharedQueueHandler sharedQueueHandler,
            IOptions<JobHostOptions> jobHostOptions,
            IOptions<FunctionResultAggregatorOptions> aggregatorOptions,
            IOptions<JobHostQueuesOptions> queueOptions,
            IOptions<JobHostBlobsOptions> blobsConfiguration,
            IServiceProvider serviceProvider)
        {
            _functionExecutor = functionExecutor;
            _functionIndexProvider = functionIndexProvider;
            _triggerBindingProvider = triggerBindingProvider;
            _singletonManager = singletonManager;
            _activator = activator;
            _hostIdProvider = hostIdProvider;
            _nameResolver = nameResolver;
            _extensions = extensions;
            _storageAccountProvider = storageAccountProvider;
            _loggerFactory = loggerFactory;
            _aggregatorFactory = aggregatorFactory;
            _queueConfiguration = queueOptions;
            _exceptionHandler = exceptionHandler;
            _sharedQueueHandler = sharedQueueHandler;
            _jobHostOptions = jobHostOptions;
            _aggregatorOptions = aggregatorOptions;
            _blobsConfiguration = blobsConfiguration;
            _serviceProvider = serviceProvider;
        }

        public async Task<JobHostContext> Create(CancellationToken shutdownToken, CancellationToken cancellationToken)
        {
            RegisterBuiltInExtensions();

            // Create the aggregator if all the pieces are configured
            IAsyncCollector<FunctionInstanceLogEntry> aggregator = null;
            FunctionResultAggregatorOptions aggregatorOptions = _aggregatorOptions.Value;
            if (_loggerFactory != null && _aggregatorFactory != null && aggregatorOptions.IsEnabled)
            {
                aggregator = _aggregatorFactory.Create(aggregatorOptions.BatchSize, aggregatorOptions.FlushTimeout, _loggerFactory);
            }

            var blobsConfiguration = _blobsConfiguration.Value;

            IAsyncCollector<FunctionInstanceLogEntry> registeredFunctionEventCollector = _serviceProvider.GetService<IAsyncCollector<FunctionInstanceLogEntry>>();

            IAsyncCollector<FunctionInstanceLogEntry> functionEventCollector;
            if (registeredFunctionEventCollector != null && aggregator != null)
            {
                // If there are both an aggregator and a registered FunctionEventCollector, wrap them in a composite
                functionEventCollector = new CompositeFunctionEventCollector(new[] { registeredFunctionEventCollector, aggregator });
            }
            else
            {
                // Otherwise, take whichever one is null (or use null if both are)
                functionEventCollector = aggregator ?? registeredFunctionEventCollector;
            }

            bool hasFastTableHook = registeredFunctionEventCollector != null;
            bool noDashboardStorage = _storageAccountProvider.DashboardConnectionString == null;

            // Only testing will override these interfaces. 
            IHostInstanceLoggerProvider hostInstanceLoggerProvider = _serviceProvider.GetService<IHostInstanceLoggerProvider>();
            IFunctionInstanceLoggerProvider functionInstanceLoggerProvider = _serviceProvider.GetService<IFunctionInstanceLoggerProvider>();
            IFunctionOutputLoggerProvider functionOutputLoggerProvider = _serviceProvider.GetService<IFunctionOutputLoggerProvider>();

            using (CancellationTokenSource combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, shutdownToken))
            {
                CancellationToken combinedCancellationToken = combinedCancellationSource.Token;

                await WriteSiteExtensionManifestAsync(combinedCancellationToken);

                IStorageAccount dashboardAccount = await _storageAccountProvider.GetDashboardAccountAsync(combinedCancellationToken);

                IHostInstanceLogger hostInstanceLogger = await hostInstanceLoggerProvider.GetAsync(combinedCancellationToken);
                IFunctionInstanceLogger functionInstanceLogger = await functionInstanceLoggerProvider.GetAsync(combinedCancellationToken);
                IFunctionOutputLogger functionOutputLogger = await functionOutputLoggerProvider.GetAsync(combinedCancellationToken);

                // TODO: FACAVAL: Chat with Brettsam, this should probably be moved out of here.
                _loggerFactory.AddProvider(new FunctionOutputLoggerProvider());

                IFunctionIndex functions = await _functionIndexProvider.GetAsync(combinedCancellationToken);
                IListenerFactory functionsListenerFactory = new HostListenerFactory(functions.ReadAll(), _singletonManager, _activator, _nameResolver, _loggerFactory);

                IFunctionExecutor hostCallExecutor;
                IListener listener;
                HostOutputMessage hostOutputMessage;

                string hostId = await _hostIdProvider.GetHostIdAsync(cancellationToken);
                if (string.Compare(_jobHostOptions.Value.HostId, hostId, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    // if this isn't a static host ID, provide the HostId on the config
                    // so it is accessible
                    _jobHostOptions.Value.HostId = hostId;
                }

                if (dashboardAccount == null)
                {
                    hostCallExecutor = new ShutdownFunctionExecutor(shutdownToken, _functionExecutor);

                    IListener factoryListener = new ListenerFactoryListener(functionsListenerFactory, _sharedQueueHandler);
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
                        _queueConfiguration.Value, _exceptionHandler, _loggerFactory, functions,
                        functionInstanceLogger, _functionExecutor);

                    Guid hostInstanceId = Guid.NewGuid();
                    string instanceQueueName = HostQueueNames.GetHostQueueName(hostInstanceId.ToString("N"));
                    IStorageQueue instanceQueue = dashboardQueueClient.GetQueueReference(instanceQueueName);
                    IListenerFactory instanceQueueListenerFactory = new HostMessageListenerFactory(instanceQueue,
                        _queueConfiguration.Value, _exceptionHandler, _loggerFactory, functions,
                        functionInstanceLogger, _functionExecutor);

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
                    string displayName = hostAssembly != null ? AssemblyNameCache.GetName(hostAssembly).Name : "Unknown";

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
                        _exceptionHandler, shutdownToken, _functionExecutor);
                    IListenerFactory hostListenerFactory = new CompositeListenerFactory(functionsListenerFactory,
                        sharedQueueListenerFactory, instanceQueueListenerFactory);
                    listener = CreateHostListener(hostListenerFactory, _sharedQueueHandler, heartbeatCommand, _exceptionHandler, shutdownToken);

                    // Publish this to Azure logging account so that a web dashboard can see it. 
                    await LogHostStartedAsync(functions, hostOutputMessage, hostInstanceLogger, combinedCancellationToken);
                }

                if (_functionExecutor is FunctionExecutor executor)
                {
                    executor.HostOutputMessage = hostOutputMessage;
                }

                IEnumerable<FunctionDescriptor> descriptors = functions.ReadAllDescriptors();
                int descriptorsCount = descriptors.Count();

                ILogger startupLogger = _loggerFactory?.CreateLogger(LogCategories.Startup);

                if (_jobHostOptions.Value.UsingDevelopmentSettings)
                {
                    string msg = "Development settings applied";
                    startupLogger?.LogDebug(msg);
                }

                if (descriptorsCount == 0)
                {
                    string msg = string.Format("No job functions found. Try making your job classes and methods public. {0}",
                        Constants.ExtensionInitializationMessage);

                    startupLogger?.LogWarning(msg);
                }
                else
                {
                    StringBuilder functionsTrace = new StringBuilder();
                    functionsTrace.AppendLine("Found the following functions:");

                    foreach (FunctionDescriptor descriptor in descriptors)
                    {
                        functionsTrace.AppendLine(descriptor.FullName);
                    }
                    string msg = functionsTrace.ToString();
                    startupLogger?.LogInformation(msg);
                }

                return new JobHostContext(
                    functions,
                    hostCallExecutor,
                    listener,
                    functionEventCollector,
                    _loggerFactory);
            }
        }

        private void RegisterBuiltInExtensions()
        {
            bool builtinsAdded = _extensions.GetExtensions<IExtensionConfigProvider>().OfType<TableExtension>().Any();

            if (!builtinsAdded)
            {
                _extensions.RegisterExtension<IExtensionConfigProvider>(_serviceProvider.GetService<TableExtension>());
                _extensions.RegisterExtension<IExtensionConfigProvider>(_serviceProvider.GetService<QueueExtension>());
                _extensions.RegisterExtension<IExtensionConfigProvider>(_serviceProvider.GetService<BlobExtensionConfig>());
                _extensions.RegisterExtension<IExtensionConfigProvider>(_serviceProvider.GetService<BlobTriggerExtensionConfig>());
            }

            IConverterManager converterManager = _serviceProvider.GetService<IConverterManager>();
            IWebHookProvider webHookProvider = _serviceProvider.GetService<IWebHookProvider>();
            ExtensionConfigContext context = new ExtensionConfigContext(converterManager, webHookProvider, _extensions)
            {
                Config = _jobHostOptions.Value
            };
            InvokeExtensionConfigProviders(context);
        }

        private void InvokeExtensionConfigProviders(ExtensionConfigContext context)
        {
            IEnumerable<IExtensionConfigProvider> configProviders = _extensions.GetExtensions(typeof(IExtensionConfigProvider)).Cast<IExtensionConfigProvider>();
            foreach (IExtensionConfigProvider configProvider in configProviders)
            {
                context.Current = configProvider;
                configProvider.Initialize(context);
            }
            context.ApplyRules();
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private static IListener CreateHostListener(IListenerFactory allFunctionsListenerFactory, SharedQueueHandler sharedQueue,
            IRecurrentCommand heartbeatCommand, IWebJobsExceptionHandler exceptionHandler, CancellationToken shutdownToken)
        {
            IListener factoryListener = new ListenerFactoryListener(allFunctionsListenerFactory, sharedQueue);
            IListener heartbeatListener = new HeartbeatListener(heartbeatCommand, exceptionHandler, factoryListener);
            IListener shutdownListener = new ShutdownListener(shutdownToken, heartbeatListener);
            return shutdownListener;
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

        private class DataOnlyHostOutputMessage : HostOutputMessage
        {
            internal override void AddMetadata(IDictionary<string, string> metadata)
            {
                throw new NotSupportedException();
            }
        }
    }
}