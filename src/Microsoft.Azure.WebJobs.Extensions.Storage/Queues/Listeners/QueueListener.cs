// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Microsoft.Azure.WebJobs.Host.Queues.Listeners
{
    internal sealed class QueueListener : IListener, ITaskSeriesCommand, INotificationCommand
    {
        private readonly ITaskSeriesTimer _timer;
        private readonly IDelayStrategy _delayStrategy;
        private readonly CloudQueue _queue;
        private readonly CloudQueue _poisonQueue;
        private readonly ITriggerExecutor<CloudQueueMessage> _triggerExecutor;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly IMessageEnqueuedWatcher _sharedWatcher;
        private readonly List<Task> _processing = new List<Task>();
        private readonly object _stopWaitingTaskSourceLock = new object();
        private readonly JobHostQueuesOptions _queueuOptions;
        private readonly QueueProcessor _queueProcessor;
        private readonly TimeSpan _visibilityTimeout;

        private bool _foundMessageSinceLastDelay;
        private bool _disposed;
        private TaskCompletionSource<object> _stopWaitingTaskSource;

        public QueueListener(CloudQueue queue,
            CloudQueue poisonQueue,
            ITriggerExecutor<CloudQueueMessage> triggerExecutor,
            IWebJobsExceptionHandler exceptionHandler,
            ILoggerFactory loggerFactory,
            SharedQueueWatcher sharedWatcher,
            JobHostQueuesOptions queueOptions,
            QueueProcessor queueProcessor = null,
            TimeSpan? maxPollingInterval = null)
        {
            if (queueOptions == null)
            {
                throw new ArgumentNullException("queueConfiguration");
            }

            if (queueOptions.BatchSize <= 0)
            {
                throw new ArgumentException("BatchSize must be greater than zero.");
            }

            if (queueOptions.MaxDequeueCount <= 0)
            {
                throw new ArgumentException("MaxDequeueCount must be greater than zero.");
            }

            _timer = new TaskSeriesTimer(this, exceptionHandler, Task.Delay(0));
            _queue = queue;
            _poisonQueue = poisonQueue;
            _triggerExecutor = triggerExecutor;
            _exceptionHandler = exceptionHandler;
            _queueuOptions = queueOptions;

            // if the function runs longer than this, the invisibility will be updated
            // on a timer periodically for the duration of the function execution
            _visibilityTimeout = TimeSpan.FromMinutes(10);

            if (sharedWatcher != null)
            {
                // Call Notify whenever a function adds a message to this queue.
                sharedWatcher.Register(queue.Name, this);
                _sharedWatcher = sharedWatcher;
            }

            EventHandler<PoisonMessageEventArgs> poisonMessageEventHandler = _sharedWatcher != null ? OnMessageAddedToPoisonQueue : (EventHandler<PoisonMessageEventArgs>)null;
            _queueProcessor = queueProcessor ?? CreateQueueProcessor(
                _queue, _poisonQueue,
                loggerFactory, _queueuOptions, poisonMessageEventHandler);

            TimeSpan maximumInterval = _queueProcessor.MaxPollingInterval;
            if (maxPollingInterval.HasValue && maximumInterval > maxPollingInterval.Value)
            {
                // enforce the maximum polling interval if specified
                maximumInterval = maxPollingInterval.Value;
            }

            _delayStrategy = new RandomizedExponentialBackoffStrategy(QueuePollingIntervals.Minimum, maximumInterval);
        }

        // for testing
        internal TimeSpan MinimumVisibilityRenewalInterval { get; set; } = TimeSpan.FromMinutes(1);

        public void Cancel()
        {
            ThrowIfDisposed();
            _timer.Cancel();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            _timer.Start();
            return Task.FromResult(0);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            _timer.Cancel();
            await Task.WhenAll(_processing);
            await _timer.StopAsync(cancellationToken);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _timer.Dispose();
                _disposed = true;
            }
        }

        public async Task<TaskSeriesCommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            lock (_stopWaitingTaskSourceLock)
            {
                if (_stopWaitingTaskSource != null)
                {
                    _stopWaitingTaskSource.TrySetResult(null);
                }

                _stopWaitingTaskSource = new TaskCompletionSource<object>();
            }

            IEnumerable<CloudQueueMessage> batch;
            try
            {
                batch = await _queue.GetMessagesAsync(_queueProcessor.BatchSize,
                    _visibilityTimeout,
                    options: null,
                    operationContext: null,
                    cancellationToken: cancellationToken);
            }
            catch (StorageException exception)
            {
                if (exception.IsNotFoundQueueNotFound() ||
                    exception.IsConflictQueueBeingDeletedOrDisabled() ||
                    exception.IsServerSideError())
                {
                    // Back off when no message is available, or when
                    // transient errors occur
                    return CreateBackoffResult();
                }
                else
                {
                    throw;
                }
            }

            if (batch == null)
            {
                return CreateBackoffResult();
            }

            bool foundMessage = false;
            foreach (var message in batch)
            {
                if (message == null)
                {
                    continue;
                }

                foundMessage = true;

                // Note: Capturing the cancellationToken passed here on a task that continues to run is a slight abuse
                // of the cancellation token contract. However, the timer implementation would not dispose of the
                // cancellation token source until it has stopped and perhaps also disposed, and we wait for all
                // outstanding tasks to complete before stopping the timer.
                Task task = ProcessMessageAsync(message, _visibilityTimeout, cancellationToken);

                // Having both WaitForNewBatchThreshold and this method mutate _processing is safe because the timer
                // contract is serial: it only calls ExecuteAsync once the wait expires (and the wait won't expire until
                // WaitForNewBatchThreshold has finished mutating _processing).
                _processing.Add(task);
            }

            // Back off when no message was found.
            if (!foundMessage)
            {
                return CreateBackoffResult();
            }

            _foundMessageSinceLastDelay = true;
            return CreateSucceededResult();
        }

        public void Notify()
        {
            lock (_stopWaitingTaskSourceLock)
            {
                if (_stopWaitingTaskSource != null)
                {
                    _stopWaitingTaskSource.TrySetResult(null);
                }
            }
        }

        private Task CreateDelayWithNotificationTask()
        {
            Task normalDelay = Task.Delay(_delayStrategy.GetNextDelay(executionSucceeded: _foundMessageSinceLastDelay));
            _foundMessageSinceLastDelay = false;
            return Task.WhenAny(_stopWaitingTaskSource.Task, normalDelay);
        }

        private TaskSeriesCommandResult CreateBackoffResult()
        {
            return new TaskSeriesCommandResult(wait: CreateDelayWithNotificationTask());
        }

        private TaskSeriesCommandResult CreateSucceededResult()
        {
            Task wait = WaitForNewBatchThreshold();
            return new TaskSeriesCommandResult(wait);
        }

        private async Task WaitForNewBatchThreshold()
        {
            while (_processing.Count > _queueProcessor.NewBatchThreshold)
            {
                Task processed = await Task.WhenAny(_processing);
                _processing.Remove(processed);
            }
        }

        internal async Task ProcessMessageAsync(CloudQueueMessage message, TimeSpan visibilityTimeout, CancellationToken cancellationToken)
        {
            try
            {
                if (!await _queueProcessor.BeginProcessingMessageAsync(message, cancellationToken))
                {
                    return;
                }

                FunctionResult result = null;
                using (ITaskSeriesTimer timer = CreateUpdateMessageVisibilityTimer(_queue, message, visibilityTimeout, _exceptionHandler))
                {
                    timer.Start();

                    result = await _triggerExecutor.ExecuteAsync(message, cancellationToken);

                    await timer.StopAsync(cancellationToken);
                }

                await _queueProcessor.CompleteProcessingMessageAsync(message, result, cancellationToken);
            }
            catch (StorageException ex) when (ex.IsTaskCanceled())
            {
                // TaskCanceledExceptions may be wrapped in StorageException.
            }
            catch (OperationCanceledException)
            {
                // Don't fail the top-level task when an inner task cancels.
            }
            catch (Exception exception)
            {
                // Immediately report any unhandled exception from this background task.
                // (Don't capture the exception as a fault of this Task; that would delay any exception reporting until
                // Stop is called, which might never happen.)
                _exceptionHandler.OnUnhandledExceptionAsync(ExceptionDispatchInfo.Capture(exception)).GetAwaiter().GetResult();
            }
        }

        private void OnMessageAddedToPoisonQueue(object sender, PoisonMessageEventArgs e)
        {
            // TODO: this is assuming that the poison queue is in the same
            // storage account
            _sharedWatcher.Notify(e.PoisonQueue.Name);
        }

        private ITaskSeriesTimer CreateUpdateMessageVisibilityTimer(CloudQueue queue,
            CloudQueueMessage message, TimeSpan visibilityTimeout,
            IWebJobsExceptionHandler exceptionHandler)
        {
            // Update a message's visibility when it is halfway to expiring.
            TimeSpan normalUpdateInterval = new TimeSpan(visibilityTimeout.Ticks / 2);

            IDelayStrategy speedupStrategy = new LinearSpeedupStrategy(normalUpdateInterval, MinimumVisibilityRenewalInterval);
            ITaskSeriesCommand command = new UpdateQueueMessageVisibilityCommand(queue, message, visibilityTimeout, speedupStrategy);
            return new TaskSeriesTimer(command, exceptionHandler, Task.Delay(normalUpdateInterval));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }
        }

        internal static QueueProcessor CreateQueueProcessor(CloudQueue queue, CloudQueue poisonQueue, ILoggerFactory loggerFactory,
            JobHostQueuesOptions queueConfig, EventHandler<PoisonMessageEventArgs> poisonQueueMessageAddedHandler)
        {
            QueueProcessorFactoryContext context = new QueueProcessorFactoryContext(queue, loggerFactory, queueConfig, poisonQueue);

            QueueProcessor queueProcessor = null;
            if (HostQueueNames.IsHostQueue(queue.Name) &&
                string.Compare(queue.Uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) != 0)
            {
                // We only delegate to the processor factory for application queues,
                // not our built in control queues
                // We bypass this check for local testing though
                queueProcessor = new QueueProcessor(context);
            }
            else
            {
                queueProcessor = queueConfig.QueueProcessorFactory.Create(context);
            }

            if (poisonQueueMessageAddedHandler != null)
            {
                queueProcessor.MessageAddedToPoisonQueue += poisonQueueMessageAddedHandler;
            }

            return queueProcessor;
        }
    }
}
