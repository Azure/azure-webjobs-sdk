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
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Dispatch;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


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
        private readonly IExtensionTypeLocator _extensionTypeLocator;
        private readonly IStorageAccountProvider _storageAccountProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOptions<JobHostQueuesOptions> _queueConfiguration;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly SharedQueueHandler _sharedQueueHandler;
        private readonly IOptions<JobHostOptions> _jobHostOptions;
        private readonly IOptions<JobHostBlobsOptions> _blobsConfiguration;
        private readonly IHostInstanceLogger _hostInstanceLogger;
        private readonly IFunctionInstanceLogger _functionInstanceLogger;
        private readonly IFunctionOutputLogger _functionOutputLogger;
        private readonly IConverterManager _converterManager;
        private readonly IAsyncCollector<FunctionInstanceLogEntry> _eventCollector;

        public JobHostContextFactory(IFunctionExecutor functionExecutor,
            IFunctionIndexProvider functionIndexProvider,
            ITriggerBindingProvider triggerBindingProvider,
            SingletonManager singletonManager,
            IJobActivator activator,
            IHostIdProvider hostIdProvider,
            INameResolver nameResolver,
            IExtensionRegistry extensions,
            IExtensionTypeLocator extensionTypeLocator,
            IStorageAccountProvider storageAccountProvider,
            ILoggerFactory loggerFactory,
            IWebJobsExceptionHandler exceptionHandler,
            SharedQueueHandler sharedQueueHandler,
            IOptions<JobHostOptions> jobHostOptions,
            IOptions<JobHostQueuesOptions> queueOptions,
            IOptions<JobHostBlobsOptions> blobsConfiguration,
            IHostInstanceLogger hostInstanceLogger,
            IFunctionInstanceLogger functionInstanceLogger,
            IFunctionOutputLogger functionOutputLogger,
            IConverterManager converterManager,
            IAsyncCollector<FunctionInstanceLogEntry> eventCollector)
        {
            _functionExecutor = functionExecutor;
            _functionIndexProvider = functionIndexProvider;
            _triggerBindingProvider = triggerBindingProvider;
            _singletonManager = singletonManager;
            _activator = activator;
            _hostIdProvider = hostIdProvider;
            _nameResolver = nameResolver;
            _extensions = extensions;
            _extensionTypeLocator = extensionTypeLocator;
            _storageAccountProvider = storageAccountProvider;
            _loggerFactory = loggerFactory;
            _queueConfiguration = queueOptions;
            _exceptionHandler = exceptionHandler;
            _sharedQueueHandler = sharedQueueHandler;
            _jobHostOptions = jobHostOptions;
            _blobsConfiguration = blobsConfiguration;
            _hostInstanceLogger = hostInstanceLogger;
            _functionInstanceLogger = functionInstanceLogger;
            _functionOutputLogger = functionOutputLogger;
            _converterManager = converterManager;
            _eventCollector = eventCollector;
        }

        public async Task<JobHostContext> Create(CancellationToken shutdownToken, CancellationToken cancellationToken)
        {
            using (CancellationTokenSource combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, shutdownToken))
            {
                CancellationToken combinedCancellationToken = combinedCancellationSource.Token;

                AddStreamConverters(_extensionTypeLocator, _converterManager);

                await WriteSiteExtensionManifestAsync(combinedCancellationToken);

                IStorageAccount dashboardAccount = await _storageAccountProvider.GetDashboardAccountAsync(combinedCancellationToken);

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
                        _functionInstanceLogger, _functionExecutor);

                    Guid hostInstanceId = Guid.NewGuid();
                    string instanceQueueName = HostQueueNames.GetHostQueueName(hostInstanceId.ToString("N"));
                    IStorageQueue instanceQueue = dashboardQueueClient.GetQueueReference(instanceQueueName);
                    IListenerFactory instanceQueueListenerFactory = new HostMessageListenerFactory(instanceQueue,
                        _queueConfiguration.Value, _exceptionHandler, _loggerFactory, functions,
                        _functionInstanceLogger, _functionExecutor);

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
                    await LogHostStartedAsync(functions, hostOutputMessage, _hostInstanceLogger, combinedCancellationToken);
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
                    _eventCollector,
                    _loggerFactory);
            }
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

        #region Backwards compat shim for ExtensionLocator
        // We can remove this when we fix https://github.com/Azure/azure-webjobs-sdk/issues/995

        // create IConverterManager adapters to any legacy ICloudBlobStreamBinder<T>. 
        private static void AddStreamConverters(IExtensionTypeLocator extensionTypeLocator, IConverterManager cm)
        {
            if (extensionTypeLocator == null)
            {
                return;
            }

            foreach (var type in extensionTypeLocator.GetCloudBlobStreamBinderTypes())
            {
                var instance = Activator.CreateInstance(type);

                var bindingType = Blobs.CloudBlobStreamObjectBinder.GetBindingValueType(type);
                var method = typeof(JobHostContextFactory).GetMethod("AddAdapter", BindingFlags.Static | BindingFlags.NonPublic);
                method = method.MakeGenericMethod(bindingType);
                method.Invoke(null, new object[] { cm, instance });
            }
        }

        private static void AddAdapter<T>(ConverterManager cm, ICloudBlobStreamBinder<T> x)
        {
            cm.AddExactConverter<Stream, T>(stream => x.ReadFromStreamAsync(stream, CancellationToken.None).Result);

            cm.AddExactConverter<ApplyConversion<T, Stream>, object>(pair =>
            {
                T value = pair.Value;
                Stream stream = pair.Existing;
                x.WriteToStreamAsync(value, stream, CancellationToken.None).Wait();
                return null;
            });
        }
        #endregion

        private class DataOnlyHostOutputMessage : HostOutputMessage
        {
            internal override void AddMetadata(IDictionary<string, string> metadata)
            {
                throw new NotSupportedException();
            }
        }
    }
}