// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus.Listeners
{
    internal sealed class ServiceBusListener : IListener
    {
        private readonly MessagingProvider _messagingProvider;
        private readonly string _entityPath;
        private readonly ServiceBusTriggerExecutor _triggerExecutor;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly MessageProcessor _messageProcessor;
        private readonly MessagingExceptionHandler _exceptionHandler;
        private MessagingFactory _messagingFactory;
        private MessageReceiver _receiver;
        private bool _disposed;
        private bool _started;

        public ServiceBusListener(MessagingFactory messagingFactory, string entityPath, ServiceBusTriggerExecutor triggerExecutor, ServiceBusConfiguration config, MessagingExceptionHandler exceptionHandler)
        {
            _messagingFactory = messagingFactory;
            _entityPath = entityPath;
            _triggerExecutor = triggerExecutor;
            _cancellationTokenSource = new CancellationTokenSource();
            _messagingProvider = config.MessagingProvider;
            _messageProcessor = config.MessagingProvider.CreateMessageProcessor(entityPath);
            _exceptionHandler = exceptionHandler;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_started)
            {
                throw new InvalidOperationException("The listener has already been started.");
            }

            _receiver = _messagingProvider.CreateMessageReceiver(_messagingFactory, _entityPath);
            _receiver.OnMessageAsync(ProcessMessageAsync, _messageProcessor.MessageOptions);
            _started = true;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            // important to disable the exception handler BEFORE aborting
            // the messaging factory, to avoid spurious error logs due
            // to receive attempts on a closed connection, etc.
            _exceptionHandler.Unsubscribe();

            if (!_started)
            {
                throw new InvalidOperationException("The listener has not yet been started or has already been stopped.");
            }

            // cancel our token source to signal any in progress
            // ProcessMessageAsync invocations to cancel
            _cancellationTokenSource.Cancel();

            // abort the message factory which will stop all receivers
            // created by it so no new work is started
            _messagingFactory.Abort();

            _started = false;

            return Task.CompletedTask;
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            _cancellationTokenSource.Cancel();
        }

        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_cancellationTokenSource")]
        public void Dispose()
        {
            if (!_disposed)
            {
                // Running callers might still be using the cancellation token.
                // Mark it canceled but don't dispose of the source while the callers are running.
                // Otherwise, callers would receive ObjectDisposedException when calling token.Register.
                // For now, rely on finalization to clean up _cancellationTokenSource's wait handle (if allocated).
                _cancellationTokenSource.Cancel();

                if (_messagingFactory != null)
                {
                    _messagingFactory.Abort();
                    _messagingFactory = null;
                }

                // Aborting the messaging factory aborts the receiver as well
                _receiver = null;

                _disposed = true;
            }
        }

        private Task ProcessMessageAsync(BrokeredMessage message)
        {
            return ProcessMessageAsync(message, _cancellationTokenSource.Token);
        }

        internal async Task ProcessMessageAsync(BrokeredMessage message, CancellationToken cancellationToken)
        {
            if (!await _messageProcessor.BeginProcessingMessageAsync(message, cancellationToken))
            {
                return;
            }

            FunctionResult result = await _triggerExecutor.ExecuteAsync(message, cancellationToken);

            await _messageProcessor.CompleteProcessingMessageAsync(message, result, cancellationToken);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }
        }
    }
}
