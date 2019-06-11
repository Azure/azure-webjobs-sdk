// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
        private readonly ITriggeredFunctionExecutor _triggerExecutor;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly MessageProcessor _messageProcessor;
        private readonly ServiceBusAccount _serviceBusAccount;
        private readonly ServiceBusOptions _serviceBusOptions;
        private MessageReceiver _receiver;
        private ClientEntity _clientEntity;
        private bool _disposed;
        private bool _started;
        private IMessageSession _messageSession;
        private SessionMessageProcessor _sessionMessageProcessor;
        private readonly bool _singleDispatch;

        public ServiceBusListener(ITriggeredFunctionExecutor triggerExecutor, ServiceBusOptions config, ServiceBusAccount serviceBusAccount, MessagingProvider messagingProvider, bool singleDispatch)
        {
            _triggerExecutor = triggerExecutor;
            _cancellationTokenSource = new CancellationTokenSource();
            _messagingProvider = messagingProvider;
            _serviceBusAccount = serviceBusAccount;
            _singleDispatch = singleDispatch;

            if (!singleDispatch && serviceBusAccount.IsSessionsEnabled)
            {
                throw new InvalidOperationException("Batch triggering is not supported for the sessions enabled entity.");
            }

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

        internal IMessageSession MessageSession => _messageSession;

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

                if (_clientEntity is QueueClient queueClient)
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
                if (_singleDispatch)
                {
                    _receiver = _messagingProvider.CreateMessageReceiver(_serviceBusAccount.EntityPath, _serviceBusAccount.ConnectionString);
                    _receiver.RegisterMessageHandler(ProcessMessageAsync, _serviceBusOptions.MessageHandlerOptions);
                 }
                else
                {
                    _receiver = _messagingProvider.CreateMessageReceiver(_serviceBusAccount.EntityPath, _serviceBusAccount.ConnectionString);
                    StartMultipleMessageProcessing(cancellationToken);
                }
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

            ServiceBusTriggerInput input = ServiceBusTriggerInput.New(message);
            input.MessageReceiver = _receiver;

            FunctionResult result = await _triggerExecutor.TryExecuteAsync(GetTriggerFunctionData(message, input), cancellationToken);
            await _messageProcessor.CompleteProcessingMessageAsync(message, result, cancellationToken);
        }

        internal async Task ProcessSessionMessageAsync(IMessageSession session, Message message, CancellationToken cancellationToken)
        {
            _messageSession = session;
            if (!await _sessionMessageProcessor.BeginProcessingMessageAsync(session, message, cancellationToken))
            {
                return;
            }

            ServiceBusTriggerInput input = ServiceBusTriggerInput.New(message);
            input.MessageSession = session;

            FunctionResult result = await _triggerExecutor.TryExecuteAsync(GetTriggerFunctionData(message, input), cancellationToken);
            await _sessionMessageProcessor.CompleteProcessingMessageAsync(session, message, result, cancellationToken);
        }

        internal void StartMultipleMessageProcessing(CancellationToken cancellationToken)
        {
            Thread thread = new Thread(async () =>
            {
                while (true)
                {
                    if (_receiver == null || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    IList<Message> messages = null;
                    try
                    {
                        messages = await _receiver.ReceiveAsync(_serviceBusOptions.BatchOptions.MaxMessageCount, _serviceBusOptions.BatchOptions.OperationTimeout);
                    }
                    catch (ObjectDisposedException)
                    {
                        // best affort
                    }
                    if (messages != null)
                    {
                        Message[] messagesArray = messages.ToArray();
                        ServiceBusTriggerInput input = new ServiceBusTriggerInput()
                        {
                            Messages = messagesArray
                        };
                        input.MessageReceiver = _receiver;

                        await _triggerExecutor.TryExecuteAsync(GetTriggerFunctionData(messagesArray, input), cancellationToken);
                        Task[] completeTasks = messages.Select(x =>
                        {
                            Task task = new Task(async () =>
                            {
                                await _receiver.CompleteAsync(x.SystemProperties.LockToken);
                            });
                            task.Start();
                            return task;
                        }).ToArray();

                        await Task.WhenAll(completeTasks);
                    }

                    await Task.Delay(_serviceBusOptions.BatchOptions.DelayBetweenOperations);
                }
            });

            thread.Start();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }
        }

        private TriggeredFunctionData GetTriggerFunctionData(Message message, ServiceBusTriggerInput input)
        {
            Guid? parentId = ServiceBusCausalityHelper.GetOwner(message);
            return new TriggeredFunctionData()
            {
                ParentId = parentId,
                TriggerValue = input,
                TriggerDetails = new Dictionary<string, string>()
                {
                    { "MessageId", message.MessageId },
                    { "DeliveryCount", message.SystemProperties.DeliveryCount.ToString() },
                    { "EnqueuedTime", message.SystemProperties.EnqueuedTimeUtc.ToString() },
                    { "LockedUntil", message.SystemProperties.LockedUntilUtc.ToString() },
                    { "SessionId", message.SessionId }
                }
            };
        }

        private TriggeredFunctionData GetTriggerFunctionData(Message[] messages, ServiceBusTriggerInput input)
        {
            Guid? parentId = ServiceBusCausalityHelper.GetOwner(messages[0]);

            int length = messages.Length;
            string[] messageIds = new string[length];
            int[] deliveryCounts = new int[length];
            DateTime[] enqueuedTimes = new DateTime[length];
            DateTime[] lockedUntils = new DateTime[length];
            string[] sessionIds = new string[length];
            for (int i=0; i < messages.Length; i++)
            {
                messageIds[i] = messages[i].MessageId;
                deliveryCounts[i] = messages[i].SystemProperties.DeliveryCount;
                enqueuedTimes[i] = messages[i].SystemProperties.EnqueuedTimeUtc;
                lockedUntils[i] = messages[i].SystemProperties.LockedUntilUtc;
                sessionIds[i] = messages[i].SessionId;
            }

            return new TriggeredFunctionData()
            {
                ParentId = parentId,
                TriggerValue = input,
                TriggerDetails = new Dictionary<string, string>()
                {
                    { "MessageIds", string.Join(",", messageIds)},
                    { "DeliveryCounts", string.Join(",", deliveryCounts) },
                    { "EnqueuedTimes", string.Join(",", enqueuedTimes) },
                    { "LockedUntils", string.Join(",", lockedUntils) },
                    { "SessionIds", string.Join(",", sessionIds) }
                }
            };
        }
    }
}
