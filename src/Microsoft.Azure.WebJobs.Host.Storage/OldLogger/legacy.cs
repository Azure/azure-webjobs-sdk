// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Dispatch;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebJobs.Host.Storage.OldLogger
{
    // $$$ 
    // Wires up V1 legacy logging 
    class Legacy : ILegacyLogger
    {
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IFunctionInstanceLogger _functionInstanceLogger;
        private readonly IFunctionExecutor _functionExecutor;
        private readonly SharedQueueHandler _sharedQueueHandler;
        private readonly ILoadBalancerQueue _storageServices;
        private readonly LegacyConfig _storageAccountProvider;

        public Legacy(
            LegacyConfig storageAccountProvider,
                IWebJobsExceptionHandler exceptionHandler,
                ILoggerFactory loggerFactory,
                IFunctionInstanceLogger functionInstanceLogger,
                IFunctionExecutor functionExecutor,
                SharedQueueHandler sharedQueueHandler,
                ILoadBalancerQueue storageServices
            )
        {
            _storageAccountProvider = storageAccountProvider;
            _exceptionHandler = exceptionHandler;
            _loggerFactory = loggerFactory;
            _functionInstanceLogger = functionInstanceLogger;
            _functionExecutor = functionExecutor;
            _sharedQueueHandler = sharedQueueHandler;
            _storageServices = storageServices;
        }

        public bool Init(
            IFunctionIndex functions, 
            IListenerFactory functionsListenerFactory, 
            out IFunctionExecutor hostCallExecutor, 
            out IListener listener, 
            out HostOutputMessage hostOutputMessage, 
            string hostId, 
            CancellationToken shutdownToken)
        {
            string sharedQueueName = HostQueueNames.GetHostQueueName(hostId);
            var sharedQueue = sharedQueueName;

            IListenerFactory sharedQueueListenerFactory = new HostMessageListenerFactory(_storageServices, sharedQueue,
                 _exceptionHandler, _loggerFactory, functions,
                _functionInstanceLogger, _functionExecutor);


            Guid hostInstanceId = Guid.NewGuid();
            string instanceQueueName = HostQueueNames.GetHostQueueName(hostInstanceId.ToString("N"));
            var instanceQueue = instanceQueueName;
            IListenerFactory instanceQueueListenerFactory = new HostMessageListenerFactory(_storageServices, instanceQueue,
                _exceptionHandler, _loggerFactory, functions,
                _functionInstanceLogger, _functionExecutor);

            HeartbeatDescriptor heartbeatDescriptor = new HeartbeatDescriptor
            {
                SharedContainerName = HostContainerNames.Hosts,
                SharedDirectoryName = HostDirectoryNames.Heartbeats + "/" + hostId,
                InstanceBlobName = hostInstanceId.ToString("N"),
                ExpirationInSeconds = (int)HeartbeatIntervals.ExpirationInterval.TotalSeconds
            };

            var dashboardAccount = _storageAccountProvider.GetDashboardStorageAccount();

            var blob = dashboardAccount.CreateCloudBlobClient()
                .GetContainerReference(heartbeatDescriptor.SharedContainerName)
                .GetBlockBlobReference(heartbeatDescriptor.SharedDirectoryName + "/" + heartbeatDescriptor.InstanceBlobName);
            IRecurrentCommand heartbeatCommand = new UpdateHostHeartbeatCommand(new HeartbeatCommand(blob));

            IEnumerable<MethodInfo> indexedMethods = functions.ReadAllMethods();
            Assembly hostAssembly = JobHostContextFactory.GetHostAssembly(indexedMethods);
            string displayName = hostAssembly != null ? AssemblyNameCache.GetName(hostAssembly).Name : "Unknown";

            hostOutputMessage = new JobHostContextFactory.DataOnlyHostOutputMessage
            {
                HostInstanceId = hostInstanceId,
                HostDisplayName = displayName,
                SharedQueueName = sharedQueueName,
                InstanceQueueName = instanceQueueName,
                Heartbeat = heartbeatDescriptor,
                WebJobRunIdentifier = WebJobRunIdentifier.Current
            };

            hostCallExecutor = JobHostContextFactory.CreateHostCallExecutor(instanceQueueListenerFactory, heartbeatCommand,
                _exceptionHandler, shutdownToken, _functionExecutor);
            IListenerFactory hostListenerFactory = new CompositeListenerFactory(functionsListenerFactory,
                sharedQueueListenerFactory, instanceQueueListenerFactory);
            listener = JobHostContextFactory.CreateHostListener(hostListenerFactory, _sharedQueueHandler, heartbeatCommand, _exceptionHandler, shutdownToken);

            return true;
        }
    }
}
