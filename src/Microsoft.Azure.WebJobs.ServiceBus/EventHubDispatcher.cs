// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    internal class EventHubDispatcher : IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ActionBlock<EventHubTaskWrapper> _workQueue;

        private readonly ITriggeredFunctionExecutor _executor;
        private readonly IMessageStatusManager _statusManager;
        private readonly TimeSpan _maxElapsedTime;

        private int _messagesExecuted = 0;
        private int _messagesComplete = 0;
        private int _messagesTimeout = 0;
        private long _messagesRunning = 0;

        public EventHubDispatcher(
            ITriggeredFunctionExecutor executor,
            IMessageStatusManager statusManager,
            TimeSpan maxElapsedTime,
            int maxDop,
            int capacity)
        {
            this._executor = executor;
            this._statusManager = statusManager;
            this._maxElapsedTime = maxElapsedTime;

            _workQueue = new ActionBlock<EventHubTaskWrapper>(
                async (trigger) =>
                {
                    Interlocked.Increment(ref _messagesRunning);
                    await TriggerSingleInput(trigger.WorkItem).ConfigureAwait(false);
                    trigger.CompletionSource.SetResult(0);
                    Interlocked.Decrement(ref _messagesRunning);
                },
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = maxDop,
                    BoundedCapacity = capacity
                });
        }

        public async Task<Task> SendAsync(TriggeredFunctionData input)
        {
            var task = new EventHubTaskWrapper(input);

            // Wait for the work item to successfully enqueue
            await _workQueue.SendAsync(task, _cts.Token).ConfigureAwait(false);

            // Return a task to wait on the work item being 
            // successfully processed
            return task.CompletionSource.Task;
        }

        private async Task TriggerSingleInput(TriggeredFunctionData input)
        {
            // Change this to use the simpler form from the data pipeline
            // the continue with is unnecessary for counting and timeouts

            var startTime = Stopwatch.GetTimestamp();
            var messageId = Guid.NewGuid();

            var trigger = input.TriggerValue as EventHubTriggerInput;
            if (trigger == null)
            {
                // Only handle event hub trigger values
                return;
            }
            var message = trigger.GetSingleEventData();
            var content = trigger.GetSingleEventContent();

            // Execute with timeout (to allow more entries to flow into the long running queue                        
            var workTask = _executor.TryExecuteAsync(input, _cts.Token)
                .ContinueWith(async task => await HandleCompletion(
                    task, startTime, messageId, message, content).ConfigureAwait(false));

            var timerTask = Task.Delay(_maxElapsedTime);
            await Task.WhenAny(workTask, timerTask).ConfigureAwait(false);

            if (workTask.Status != TaskStatus.RanToCompletion)
            {
                // This task has potentially not completed.  Record the task setup 
                // information against the remote store with a max TTL
                // TODO - work against message and content
                var timestamp = Stopwatch.GetTimestamp();
                var elapsedMs = new TimeSpan(timestamp - startTime);

                await _statusManager.SetRunning(messageId,
                    TimeSpan.FromSeconds(30), elapsedMs,
                    Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(input)))
                        .ConfigureAwait(false);
                Interlocked.Increment(ref _messagesTimeout);
            }
            else
            {
                Interlocked.Increment(ref _messagesComplete);
            }

            Interlocked.Increment(ref _messagesExecuted);

            // Wait if the long running queue is too big
            while (_statusManager.ActiveTaskCount > 1024)
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }

        private async Task HandleCompletion(Task<FunctionResult> task,
            long startTime, Guid messageId, EventData message, byte[] content)
        {
            if (task.IsFaulted)
            {
                // TODO - exception handling here
                var x = task.Exception;
            }

            // Set timespan
            long timestamp = Stopwatch.GetTimestamp();
            TimeSpan elapsedMs = new TimeSpan(timestamp - startTime);

            Interlocked.Increment(ref _messagesComplete);

            // Has the task been registered in the remote store for future execution?
            // If so, signal as complete 
            await _statusManager.SetComplete(messageId, elapsedMs).ConfigureAwait(false);

            // Dispose the message to release memory as early as is practical     
            message.Dispose();
        }

        public void Dispose()
        {
            _cts.Dispose();
        }
    }
}