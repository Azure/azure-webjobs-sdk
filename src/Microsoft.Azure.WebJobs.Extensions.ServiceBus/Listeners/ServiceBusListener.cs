// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.ServiceBus.Listeners
{
    internal sealed class ServiceBusListener : IListener
    {
        private readonly MessagingProvider _messagingProvider;
        private readonly ServiceBusTriggerExecutor _triggerExecutor;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly MessageProcessor _messageProcessor;
        private readonly ServiceBusAccount _serviceBusAccount;
        private readonly ServiceBusOptions _serviceBusOptions;
        private MessageReceiver _receiver;
        private bool _disposed;
        private bool _started;

        private ClientEntity _clientEntity;
        private SessionMessageProcessor _sessionMessageProcessor;

        public ServiceBusListener(ServiceBusTriggerExecutor triggerExecutor, ServiceBusOptions config, ServiceBusAccount serviceBusAccount, MessagingProvider messagingProvider)
        {
            _triggerExecutor = triggerExecutor;
            _cancellationTokenSource = new CancellationTokenSource();
            _messagingProvider = messagingProvider;
            _serviceBusAccount = serviceBusAccount;        

            if (serviceBusAccount.IsSessionsEnabled)
            {
                _sessionMessageProcessor = _messagingProvider.CreateSessionMessageProcessor(_serviceBusAccount.EntityPath, _serviceBusAccount.ConnectionString);
            }
            else
            {
                _messageProcessor = _messagingProvider.CreateMessageProcessor(_serviceBusAccount.EntityPath, _serviceBusAccount.ConnectionString);
            }
            _serviceBusOptions = config;
        }

        internal MessageReceiver Receiver => _receiver;

        internal ClientEntity ClientEntity => _clientEntity;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_started)
            {
                throw new InvalidOperationException("The listener has already been started.");
            }

            if (_serviceBusAccount.IsSessionsEnabled)
            {
                _clientEntity = _messagingProvider.CreateClientEntity(_serviceBusAccount.EntityPath, _serviceBusAccount.ConnectionString);

                QueueClient queueClient = _clientEntity as QueueClient;
                if (queueClient != null)
                {
                    queueClient.RegisterSessionHandler(ProcessSessionMessageAsync, _serviceBusOptions.SessionHandlerOptions);
                }
                else
                {
                    SubscriptionClient subscriptionClient = _clientEntity as SubscriptionClient;
                    subscriptionClient.RegisterSessionHandler(ProcessSessionMessageAsync, _serviceBusOptions.SessionHandlerOptions);
                }
            }
            else
            {
                _receiver = _messagingProvider.CreateMessageReceiver(_serviceBusAccount.EntityPath, _serviceBusAccount.ConnectionString);
                _receiver.RegisterMessageHandler(ProcessMessageAsync, _serviceBusOptions.MessageHandlerOptions);
            }
            _started = true;

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (!_started)
            {
                throw new InvalidOperationException("The listener has not yet been started or has already been stopped.");
            }

            // cancel our token source to signal any in progress
            // ProcessMessageAsync invocations to cancel
            _cancellationTokenSource.Cancel();

            if (_receiver != null)
            {
                await _receiver.CloseAsync();
                _receiver = null;
            }
            if (_clientEntity != null)
            {
                await _clientEntity.CloseAsync();
                _clientEntity = null;
            }
            _started = false;
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
                    _receiver.CloseAsync().Wait();
                    _receiver = null;
                }

                if (_clientEntity != null)
                {
                    _clientEntity.CloseAsync().Wait();
                    _clientEntity = null;
                }

                _disposed = true;
            }
        }


        internal async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
        {
            if (!await _messageProcessor.BeginProcessingMessageAsync(message, cancellationToken))
            {
                return;
            }

            FunctionResult result = await _triggerExecutor.ExecuteAsync(message, cancellationToken);
            await _messageProcessor.CompleteProcessingMessageAsync(message, result, cancellationToken);
        }

        internal async Task ProcessSessionMessageAsync(IMessageSession session, Message message, CancellationToken cancellationToken)
        {
            if (!await _sessionMessageProcessor.BeginProcessingMessageAsync(session, message, cancellationToken))
            {
                return;
            }

            FunctionResult result = await _triggerExecutor.ExecuteAsync(message, cancellationToken);
            await _sessionMessageProcessor.CompleteProcessingMessageAsync(session, message, result, cancellationToken);
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
