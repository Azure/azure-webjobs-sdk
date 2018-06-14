﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Microsoft.Azure.WebJobs.Host.Queues.Triggers
{
    internal class QueueTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private static readonly IQueueTriggerArgumentBindingProvider InnerProvider =
            new CompositeArgumentBindingProvider(
                new ConverterArgumentBindingProvider<CloudQueueMessage>(
                    new StorageQueueMessageToCloudQueueMessageConverter()),
                new ConverterArgumentBindingProvider<string>(new StorageQueueMessageToStringConverter()),
                new ConverterArgumentBindingProvider<byte[]>(new StorageQueueMessageToByteArrayConverter()),
                new UserTypeArgumentBindingProvider()); // Must come last, because it will attempt to bind all types.

        private readonly INameResolver _nameResolver;
        private readonly IStorageAccountProvider _accountProvider;
        private readonly JobHostQueuesOptions _queueOptions;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly IContextSetter<IMessageEnqueuedWatcher> _messageEnqueuedWatcherSetter;
        private readonly ISharedContextProvider _sharedContextProvider;
        private readonly ILoggerFactory _loggerFactory;

        public QueueTriggerAttributeBindingProvider(INameResolver nameResolver,
            IStorageAccountProvider accountProvider,
            JobHostQueuesOptions queueOptions,
            IWebJobsExceptionHandler exceptionHandler,
            IContextSetter<IMessageEnqueuedWatcher> messageEnqueuedWatcherSetter,
            ISharedContextProvider sharedContextProvider,
            ILoggerFactory loggerFactory)
        {
            _accountProvider = accountProvider ?? throw new ArgumentNullException(nameof(accountProvider));
            _queueOptions = queueOptions ?? throw new ArgumentNullException(nameof(queueOptions));
            _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
            _messageEnqueuedWatcherSetter = messageEnqueuedWatcherSetter ?? throw new ArgumentNullException(nameof(messageEnqueuedWatcherSetter));
            _sharedContextProvider = sharedContextProvider ?? throw new ArgumentNullException(nameof(sharedContextProvider));

            _nameResolver = nameResolver;
            _loggerFactory = loggerFactory;
        }

        public async Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            ParameterInfo parameter = context.Parameter;
            var queueTrigger = TypeUtility.GetResolvedAttribute<QueueTriggerAttribute>(context.Parameter);

            if (queueTrigger == null)
            {
                return null;
            }

            string queueName = Resolve(queueTrigger.QueueName);
            queueName = NormalizeAndValidate(queueName);

            ITriggerDataArgumentBinding<IStorageQueueMessage> argumentBinding = InnerProvider.TryCreate(parameter);

            if (argumentBinding == null)
            {
                throw new InvalidOperationException(
                    "Can't bind QueueTrigger to type '" + parameter.ParameterType + "'.");
            }

            IStorageAccount account = await _accountProvider.GetStorageAccountAsync(queueTrigger, context.CancellationToken, _nameResolver);
            // requires storage account with queue support
            account.AssertTypeOneOf(StorageAccountType.GeneralPurpose);

            StorageClientFactoryContext clientFactoryContext = new StorageClientFactoryContext
            {
                Parameter = parameter
            };
            IStorageQueueClient client = account.CreateQueueClient(clientFactoryContext);
            IStorageQueue queue = client.GetQueueReference(queueName);

            ITriggerBinding binding = new QueueTriggerBinding(parameter.Name, queue, argumentBinding,
                _queueOptions, _exceptionHandler, _messageEnqueuedWatcherSetter,
                _sharedContextProvider, _loggerFactory);
            return binding;
        }

        private static string NormalizeAndValidate(string queueName)
        {
            queueName = queueName.ToLowerInvariant(); // must be lowercase. coerce here to be nice.
            QueueClient.ValidateQueueName(queueName);
            return queueName;
        }

        private string Resolve(string queueName)
        {
            if (_nameResolver == null)
            {
                return queueName;
            }

            return _nameResolver.ResolveWholeString(queueName);
        }
    }
}
