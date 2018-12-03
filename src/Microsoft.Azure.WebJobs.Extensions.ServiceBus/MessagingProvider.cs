// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Primitives;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    public class MessagingProvider
    {
        private readonly ServiceBusOptions _options;
        private readonly IConnectionProvider _connectionProvider;
        private readonly IConfiguration _configuration;
        private string _connectionString;
        private readonly TokenProvider _tokenProvider;

        private readonly ConcurrentDictionary<string, MessageReceiver> _messageReceiverCache = new ConcurrentDictionary<string, MessageReceiver>();
        private readonly ConcurrentDictionary<string, MessageSender> _messageSenderCache = new ConcurrentDictionary<string, MessageSender>();

        
        public MessagingProvider(IOptions<ServiceBusOptions> options, IConfiguration configuration, IConnectionProvider connectionProvider = null) : this(options)
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider;
        }

        public MessagingProvider(IOptions<ServiceBusOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            if (_options.UseManagedServiceIdentity && string.IsNullOrEmpty(_options.Endpoint))
            {
                throw new ArgumentNullException(nameof(_options.Endpoint));
            }
            _tokenProvider = _options.ServiceBusTokenProvider ?? TokenProvider.CreateManagedServiceIdentityTokenProvider();
        }


        /// <summary>
        /// Creates a <see cref="MessageProcessor"/> for the specified ServiceBus entity.
        /// </summary>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="MessageProcessor"/> for.</param>
        /// <returns>The <see cref="MessageProcessor"/>.</returns>
        public virtual MessageProcessor CreateMessageProcessor(string entityPath)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException(nameof(entityPath));
            }
            return new MessageProcessor(GetMessageReceiver(entityPath), _options.MessageHandlerOptions);
        }

        /// <summary>
        /// Creates a <see cref="MessageReceiver"/>.
        /// </summary>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="MessageReceiver"/> for.</param>
        /// <returns>The <see cref="MessageReceiver"/>.</returns>
        public MessageReceiver GetMessageReceiver(string entityPath)
        {
            string cacheKey;
            if (_options.UseManagedServiceIdentity)
            {
                cacheKey = $"{entityPath}-{_options.Endpoint}";
                return _messageReceiverCache.GetOrAdd(cacheKey,
                    new MessageReceiver(_options.Endpoint, entityPath, _tokenProvider));
            }

            cacheKey = $"{entityPath}-{ConnectionString}";
            return _messageReceiverCache.GetOrAdd(cacheKey,
                new MessageReceiver(ConnectionString, entityPath)
                {
                    PrefetchCount = _options.PrefetchCount
                });
        }

        /// <summary>
        /// Creates a <see cref="MessageSender"/>.
        /// </summary>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="MessageSender"/> for.</param>
        /// <returns>The <see cref="MessageSender"/>.</returns>
        public MessageSender CreateMessageSender(string entityPath)
        {
            string cacheKey;
            if (_options.UseManagedServiceIdentity)
            {
                cacheKey = $"{entityPath}-{_options.Endpoint}";
                return _messageSenderCache.GetOrAdd(cacheKey,
                    new MessageSender(_options.Endpoint, entityPath, _tokenProvider));
            }

            cacheKey = $"{entityPath}-{ConnectionString}";
            return _messageSenderCache.GetOrAdd(cacheKey, new MessageSender(_options.ConnectionString, entityPath));
        }

        /// <summary>
        /// Creates a <see cref="MessageReceiver"/>.
        /// </summary>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="MessageReceiver"/> for.</param>
        /// <returns>The <see cref="MessageReceiver"/>.</returns>
        public MessageReceiver CreateMessageReceiver(string entityPath)
        {
            string cacheKey;
            if (_options.UseManagedServiceIdentity)
            {
                cacheKey = $"{entityPath}-{_options.Endpoint}";
                return _messageReceiverCache.GetOrAdd(cacheKey,
                    new MessageReceiver(_options.Endpoint, entityPath, _tokenProvider)
                    {
                        PrefetchCount = _options.PrefetchCount
                    });
            }

            cacheKey = $"{entityPath}-{ConnectionString}";
            return _messageReceiverCache.GetOrAdd(cacheKey, new MessageReceiver(_options.ConnectionString, entityPath)
            {
                PrefetchCount = _options.PrefetchCount
            });
        }

        public virtual string ConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(_connectionString))
                {
                    _connectionString = _options.ConnectionString;
                    if (_connectionProvider != null && !string.IsNullOrEmpty(_connectionProvider.Connection))
                    {
                        _connectionString = _configuration.GetWebJobsConnectionString(_connectionProvider.Connection);
                    }

                    if (string.IsNullOrEmpty(_connectionString))
                    {
                        var defaultConnectionName = "AzureWebJobsServiceBus";
                        throw new InvalidOperationException(
                            string.Format(CultureInfo.InvariantCulture, "Microsoft Azure WebJobs SDK ServiceBus connection string '{0}' is missing or empty.",
                            Sanitizer.Sanitize(_connectionProvider.Connection) ?? defaultConnectionName));
                    }
                }

                return _connectionString;
            }
        }
    }
}
