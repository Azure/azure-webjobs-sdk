// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using Microsoft.Azure.ServiceBus.Core;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    /// <summary>
    /// This class provides factory methods for the creation of instances
    /// used for ServiceBus message processing.
    /// </summary>
    public class MessagingProvider
    {
        private readonly ServiceBusConfiguration _config;
        private readonly ConcurrentDictionary<string, MessageReceiver> _messageReceiverCache = new ConcurrentDictionary<string, MessageReceiver>();
        private readonly ConcurrentDictionary<string, MessageSender> _messageSenderCache = new ConcurrentDictionary<string, MessageSender>();

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="config">The <see cref="ServiceBusConfiguration"/>.</param>
        public MessagingProvider(ServiceBusConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            _config = config;
        }

        /// <summary>
        /// Creates a <see cref="MessageProcessor"/> for the specified ServiceBus entity.
        /// </summary>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="MessageProcessor"/> for.</param>
        /// <param name="connectionString">The ServiceBus connection string.</param>
        /// <returns>The <see cref="MessageProcessor"/>.</returns>
        public virtual MessageProcessor CreateMessageProcessor(string entityPath, string connectionString)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException("entityPath");
            }
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("connectionString");
            }

            return new MessageProcessor(GetOrAddMessageReceiver(entityPath, connectionString), _config.MessageOptions);
        }

        /// <summary>
        /// Creates a <see cref="MessageReceiver"/> for the specified ServiceBus entity.
        /// </summary>
        /// <remarks>
        /// You can override this method to customize the <see cref="MessageReceiver"/>.
        /// </remarks>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="MessageReceiver"/> for.</param>
        /// <param name="connectionString">The ServiceBus connection string.</param>
        /// <returns></returns>
        public virtual MessageReceiver CreateMessageReceiver(string entityPath, string connectionString)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException("entityPath");
            }
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("connectionString");
            }

            return GetOrAddMessageReceiver(entityPath, connectionString);
        }

        /// <summary>
        /// Creates a <see cref="MessageSender"/> for the specified ServiceBus entity.
        /// </summary>
        /// <remarks>
        /// You can override this method to customize the <see cref="MessageSender"/>.
        /// </remarks>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="MessageSender"/> for.</param>
        /// <param name="connectionString">The ServiceBus connection string.</param>
        /// <returns></returns>
        public virtual MessageSender CreateMessageSender(string entityPath, string connectionString)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException("entityPath");
            }
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("connectionString");
            }

            return GetOrAddMessageSender(entityPath, connectionString);
        }

        private MessageReceiver GetOrAddMessageReceiver(string entityPath, string connectionString)
        {
            string cacheKey = $"{entityPath}-{connectionString}";
            return _messageReceiverCache.GetOrAdd(cacheKey,
                new MessageReceiver(connectionString, entityPath)
                {
                    PrefetchCount = _config.PrefetchCount
                });
        }

        private MessageSender GetOrAddMessageSender(string entityPath, string connectionString)
        {
            string cacheKey = $"{entityPath}-{connectionString}";
            return _messageSenderCache.GetOrAdd(cacheKey, new MessageSender(connectionString, entityPath));
        }
    }
}
