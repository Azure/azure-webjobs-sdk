// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Queues.Listeners
{
    internal class HostMessageListenerFactory : IListenerFactory
    {
        private readonly QueueMoniker _queue;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IFunctionIndexLookup _functionLookup;
        private readonly IFunctionInstanceLogger _functionInstanceLogger;
        private readonly IFunctionExecutor _executor;
        private readonly ISuperhack _storageServices;

        public HostMessageListenerFactory(
            ISuperhack storageServices,
            QueueMoniker queue,
            IWebJobsExceptionHandler exceptionHandler,
            ILoggerFactory loggerFactory,
            IFunctionIndexLookup functionLookup,
            IFunctionInstanceLogger functionInstanceLogger,
            IFunctionExecutor executor)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _storageServices = storageServices ?? throw new ArgumentNullException(nameof(storageServices));
            _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
            _functionLookup = functionLookup ?? throw new ArgumentNullException(nameof(functionLookup));
            _functionInstanceLogger = functionInstanceLogger ?? throw new ArgumentNullException(nameof(functionInstanceLogger));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));

            _loggerFactory = loggerFactory;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public Task<IListener> CreateAsync(CancellationToken cancellationToken)
        {
            var triggerExecutor = new HostMessageExecutor(_executor, _functionLookup, _functionInstanceLogger);

            IListener listener = _storageServices.CreateQueueListenr(_queue, null, triggerExecutor.ExecuteAsync);
    
            return Task.FromResult(listener);
        }
    }
}
