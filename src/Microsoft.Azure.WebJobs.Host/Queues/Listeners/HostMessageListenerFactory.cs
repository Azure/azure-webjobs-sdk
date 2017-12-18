// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Queues.Listeners
{
    internal class HostMessageListenerFactory : IListenerFactory
    {
        private readonly IStorageQueue _queue;
        private readonly IQueueConfiguration _queueConfiguration;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IFunctionIndexLookup _functionLookup;
        private readonly IFunctionInstanceLogger _functionInstanceLogger;
        private readonly IFunctionExecutor _executor;

        public HostMessageListenerFactory(IStorageQueue queue,
            IQueueConfiguration queueConfiguration,
            IWebJobsExceptionHandler exceptionHandler,
            ILoggerFactory loggerFactory,
            IFunctionIndexLookup functionLookup,
            IFunctionInstanceLogger functionInstanceLogger,
            IFunctionExecutor executor)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _queueConfiguration = queueConfiguration ?? throw new ArgumentNullException(nameof(queueConfiguration));
            _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
            _functionLookup = functionLookup ?? throw new ArgumentNullException(nameof(functionLookup));
            _functionInstanceLogger = functionInstanceLogger ?? throw new ArgumentNullException(nameof(functionInstanceLogger));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));

            _loggerFactory = loggerFactory;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public Task<IListener> CreateAsync(CancellationToken cancellationToken)
        {
            ITriggerExecutor<IStorageQueueMessage> triggerExecutor = new HostMessageExecutor(_executor, _functionLookup, _functionInstanceLogger);

            // Provide an upper bound on the maximum polling interval for run/abort from dashboard.
            // This ensures that if users have customized this value the Dashboard will remain responsive.
            TimeSpan maxPollingInterval = QueuePollingIntervals.DefaultMaximum;

            IListener listener = new QueueListener(_queue,
                poisonQueue: null,
                triggerExecutor: triggerExecutor,
                exceptionHandler: _exceptionHandler,
                loggerFactory: _loggerFactory,
                sharedWatcher: null,
                queueConfiguration: _queueConfiguration,
                maxPollingInterval: maxPollingInterval);

            return Task.FromResult(listener);
        }
    }
}
