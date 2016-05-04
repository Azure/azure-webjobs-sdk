// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    // Write up bindinging rules for [Queue] attribute. 
    // This is fundemantentally an IAsyncCollector<IStorageQueueMessage>
    internal class QueueBindingRules
    {
        private readonly IStorageAccountProvider _accountProvider;
        private readonly IContextGetter<IMessageEnqueuedWatcher> _messageEnqueuedWatcherGetter;

        public QueueBindingRules(IStorageAccountProvider accountProvider, IContextGetter<IMessageEnqueuedWatcher> messageEnqueuedWatcherGetter)
        {
            _accountProvider = accountProvider;
            _messageEnqueuedWatcherGetter = messageEnqueuedWatcherGetter;
        }

        public IBindingProvider BuildQueueRules(
            INameResolver nameResolver,
            IConverterManager converterManager)
        {
            // IStorageQueueMessage is the core testing interface 
            converterManager.AddConverter<byte[], IStorageQueueMessage, QueueAttribute>(ConvertByteArrayCloudQueueMessage);
            converterManager.AddConverter<string, IStorageQueueMessage, QueueAttribute>(ConvertString2CloudQueueMessage);
            converterManager.AddConverter<IStorageQueueMessage, string>(ConvertCloudQueueMessage2String);
            converterManager.AddConverter<IStorageQueueMessage, byte[]>(ConvertCloudQueueMessage2ByteArray);

            converterManager.AddConverter<CloudQueueMessage, IStorageQueueMessage>(ConvertReal2CloudQueueMessage);

            var bf = new BindingFactory(nameResolver, converterManager);

            var ruleQueueOutput = bf.BindToAsyncCollector<QueueAttribute, IStorageQueueMessage>(BuildFromQueueAttribute, ToParameterDescriptorForCollector, FixerUpper);
            var ruleQueueClient = bf.BindToExactAsyncType<QueueAttribute, IStorageQueue>(BuildClientFromQueueAttributeAsync, ToParameterDescriptorForCollector, FixerUpper);
            var ruleQueueClient2 = bf.BindToExactAsyncType<QueueAttribute, CloudQueue>(BuildRealClientFromQueueAttributeAsync, ToParameterDescriptorForCollector, FixerUpper);
            var queueRules = new GenericCompositeBindingProvider<QueueAttribute>(
                (attr) => ValidateQueueAttribute(attr, nameResolver),
                ruleQueueClient, ruleQueueClient2, ruleQueueOutput);

            return queueRules;
        }

        // Hook to apply hacky rules all at once. 
        private async Task<QueueAttribute> FixerUpper(QueueAttribute attrResolved, ParameterInfo parameter, INameResolver nameResolver)
        {
            // Look for [Storage] attribute and squerrel aover 
            IStorageAccount account = await _accountProvider.GetStorageAccountAsync(parameter, CancellationToken.None, nameResolver);
            StorageClientFactoryContext clientFactoryContext = new StorageClientFactoryContext
            {
                Parameter = parameter
            };
            IStorageQueueClient client = account.CreateQueueClient(clientFactoryContext);

            // Create the queue if needed?
            var queue = client.GetQueueReference(attrResolved.QueueName);
            bool create = false;
            if (create)
            {
                await queue.CreateIfNotExistsAsync(CancellationToken.None);
            }

            // Normalize and validate? 
            string queueName = attrResolved.QueueName.ToLowerInvariant(); // must be lowercase. coerce here to be nice.
            QueueClient.ValidateQueueName(queueName);

            // Lowercase
            return new ResolvedQueueAttribute(queueName, client);
        }

        private ParameterDescriptor ToParameterDescriptorForCollector(QueueAttribute attr, ParameterInfo parameter, INameResolver nameResolver)
        {
            Task<IStorageAccount> t = Task.Run(() =>
                _accountProvider.GetStorageAccountAsync(parameter, CancellationToken.None, nameResolver));
            string accountName = t.GetAwaiter().GetResult().Credentials.AccountName;

            return new QueueParameterDescriptor
            {
                Name = parameter.Name,
                AccountName = accountName,
                QueueName = NormalizeQueueName(attr, nameResolver),
                Access = System.IO.FileAccess.Write
            };
        }

        private static string NormalizeQueueName(QueueAttribute attribute, INameResolver nameResolver)
        {
            string queueName = attribute.QueueName;
            if (nameResolver != null)
            {
                queueName = nameResolver.ResolveWholeString(queueName);
            }
            queueName = queueName.ToLowerInvariant(); // must be lowercase. coerce here to be nice.
            return queueName;
        }

        // This is a static validation (so only %% are resolved; not {} ) 
        // For runtime validation, the regular builder functions can do the resolution.
        private static void ValidateQueueAttribute(QueueAttribute attribute, INameResolver nameResolver)
        {
            string queueName = NormalizeQueueName(attribute, nameResolver);
            QueueClient.ValidateQueueName(queueName);
        }

        private IStorageQueueMessage ConvertReal2CloudQueueMessage(CloudQueueMessage arg)
        {
            return new StorageQueueMessage(arg);
        }

        private byte[] ConvertCloudQueueMessage2ByteArray(IStorageQueueMessage arg)
        {
            return arg.AsBytes;
        }

        private string ConvertCloudQueueMessage2String(IStorageQueueMessage arg)
        {
            return arg.AsString;
        }

        private IStorageQueueMessage ConvertByteArrayCloudQueueMessage(byte[] arg, QueueAttribute attrResolved)
        {
            var attr = (ResolvedQueueAttribute)attrResolved;
            var msg = attr.GetQueue().CreateMessage(arg);
            return msg;
        }

        private IStorageQueueMessage ConvertString2CloudQueueMessage(string arg, QueueAttribute attrResolved)
        {
            var attr = (ResolvedQueueAttribute)attrResolved;
            var msg = attr.GetQueue().CreateMessage(arg);
            return msg;
        }

        private async Task<CloudQueue> BuildRealClientFromQueueAttributeAsync(QueueAttribute attrResolved)
        {
            var queue = await this.BuildClientFromQueueAttributeAsync(attrResolved);
            return queue.SdkObject;
        }

        private async Task<IStorageQueue> BuildClientFromQueueAttributeAsync(QueueAttribute attrResolved)
        {
            var attr = (ResolvedQueueAttribute)attrResolved;
            var queue = attr.GetQueue();
            await queue.CreateIfNotExistsAsync(CancellationToken.None);
            return queue;
        }

        private IAsyncCollector<IStorageQueueMessage> BuildFromQueueAttribute(QueueAttribute attrResolved)
        {
            var attr = (ResolvedQueueAttribute)attrResolved;
            var queue = attr.GetQueue();
            return new QueueAsyncCollector(queue, _messageEnqueuedWatcherGetter.Value);
        }

        // Queue attributes are paired with a separate [StorageAccount]. 
        // Consolidate the information from both attributes into a single attribute.
        internal sealed class ResolvedQueueAttribute : QueueAttribute
        {
            public ResolvedQueueAttribute(string queueName, IStorageQueueClient client)
                : base(queueName)
            {
                this.Client = client;
            }

            internal IStorageQueueClient Client { get; private set; }

            public IStorageQueue GetQueue()
            {
                return this.Client.GetQueueReference(this.QueueName);
            }
        }

        // The core Async Collector for queueing messages. 
        internal class QueueAsyncCollector : IAsyncCollector<IStorageQueueMessage>
        {
            private readonly IStorageQueue _queue;
            private readonly IMessageEnqueuedWatcher _messageEnqueuedWatcher;

            public QueueAsyncCollector(IStorageQueue queue, IMessageEnqueuedWatcher messageEnqueuedWatcher)
            {
                this._queue = queue;
                this._messageEnqueuedWatcher = messageEnqueuedWatcher;
            }

            public Task AddAsync(IStorageQueueMessage message, CancellationToken cancellationToken = default(CancellationToken))
            {
                if (message == null)
                {
                    throw new InvalidOperationException("Cannot enqueue a null queue message instance.");
                }

                if (_messageEnqueuedWatcher != null)
                {
                    _messageEnqueuedWatcher.Notify(_queue.Name);
                }

                return _queue.AddMessageAndCreateIfNotExistsAsync(message, cancellationToken);
            }

            public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                // Batching not supported. 
                return Task.FromResult(0);
            }
        }
    }
}