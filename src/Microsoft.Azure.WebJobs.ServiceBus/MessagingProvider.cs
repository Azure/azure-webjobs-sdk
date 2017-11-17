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
        /// <returns>The <see cref="MessageProcessor"/>.</returns>
        public virtual MessageProcessor CreateMessageProcessor(string entityPath, string connectionString)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException("entityPath");
            }

            return new MessageProcessor(GetOrAddMessageReceiver(entityPath, connectionString), _config.MessageOptions);
        }

        /// <summary>
        /// Creates a <see cref="MessageReceiver"/> for the specified ServiceBus entity.
        /// </summary>
        /// <remarks>
        /// You can override this method to customize the <see cref="MessageReceiver"/>.
        /// </remarks>
        /// <param name="factory">The <see cref="MessagingFactory"/> to use.</param>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="MessageReceiver"/> for.</param>
        /// <returns></returns>
        public virtual MessageReceiver CreateMessageReceiver(string entityPath, string connectionString)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException("entityPath");
            }

            return GetOrAddMessageReceiver(entityPath, connectionString);
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
    }
}
