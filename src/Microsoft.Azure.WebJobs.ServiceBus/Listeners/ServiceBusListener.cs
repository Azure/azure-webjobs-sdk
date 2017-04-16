﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus.Listeners
{
    internal sealed class ServiceBusListener : IListener
    {
        private readonly MessagingProvider _messagingProvider;
        private readonly MessagingFactory _messagingFactory;
        private readonly string _entityPath;
        private readonly ServiceBusTriggerExecutor _triggerExecutor;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly MessageProcessor _messageProcessor;
        private readonly IWebJobsExceptionHandler _handler;

        private MessageReceiver _receiver;
        private bool _disposed;

        public ServiceBusListener(MessagingFactory messagingFactory, string entityPath, ServiceBusTriggerExecutor triggerExecutor, ServiceBusConfiguration config, IWebJobsExceptionHandler handler)
        {
            _messagingFactory = messagingFactory;
            _entityPath = entityPath;
            _triggerExecutor = triggerExecutor;
            _cancellationTokenSource = new CancellationTokenSource();
            _messagingProvider = config.MessagingProvider;
            _messageProcessor = config.MessagingProvider.CreateMessageProcessor(entityPath);
            _handler = handler;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_receiver != null)
            {
                throw new InvalidOperationException("The listener has already been started.");
            }

            return StartAsyncCore(cancellationToken);
        }

        private Task StartAsyncCore(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _receiver = _messagingProvider.CreateMessageReceiver(_messagingFactory, _entityPath);
            var cloned = CloneMessageOptions(_messageProcessor.MessageOptions);
            cloned.ExceptionReceived += (sender, args) => _handler.HandleAsync(args.Exception).GetAwaiter().GetResult();
            _receiver.OnMessageAsync(ProcessMessageAsync, cloned);

            return Task.FromResult(0);
        }

        internal static OnMessageOptions CloneMessageOptions(OnMessageOptions original)
        {
            var clone = new OnMessageOptions()
            {
                AutoComplete = original.AutoComplete,
                AutoRenewTimeout = original.AutoRenewTimeout,
                MaxConcurrentCalls = original.MaxConcurrentCalls
            };
            var field = typeof(OnMessageOptions).GetField("ExceptionReceived", BindingFlags.Instance | BindingFlags.NonPublic);
            var eventHandler = field.GetValue(original) as Delegate;
            if (eventHandler != null)
            {
                foreach (var subscriber in eventHandler.GetInvocationList())
                {
                    clone.ExceptionReceived += subscriber as EventHandler<ExceptionReceivedEventArgs>;
                }
            }

            return clone;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_receiver == null)
            {
                throw new InvalidOperationException(
                    "The listener has not yet been started or has already been stopped.");
            }

            // Signal ProcessMessage to shut down gracefully
            _cancellationTokenSource.Cancel();

            return StopAsyncCore(cancellationToken);
        }

        private async Task StopAsyncCore(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _receiver.CloseAsync();
            _receiver = null;
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

                if (_receiver != null)
                {
                    _receiver.Abort();
                    _receiver = null;
                }

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
