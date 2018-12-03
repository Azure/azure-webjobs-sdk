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

        private readonly ConcurrentDictionary<string, MessageReceiver> _messageReceiverCache = new ConcurrentDictionary<string, MessageReceiver>();
        private readonly ConcurrentDictionary<string, MessageSender> _messageSenderCache = new ConcurrentDictionary<string, MessageSender>();

        public MessagingProvider(IOptions<ServiceBusOptions> options, IConfiguration configuration, IConnectionProvider connectionProvider = null)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _configuration = configuration;
            _connectionProvider = connectionProvider;
        }

        public MessagingProvider(IOptions<ServiceBusOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }


        public MessageProcessor GetMessageProcessor(string entityPath)
        {
            return new MessageProcessor(GetMessageReceiver(entityPath), _options.MessageHandlerOptions);
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

        public MessageReceiver GetMessageReceiver(string entityPath)
        {
            string cacheKey;
            if (_options.UseManagedServiceIdentity)
            {
                cacheKey = $"{entityPath}-{_options.Endpoint}";
                var tokenProvider = TokenProvider.CreateManagedServiceIdentityTokenProvider();

                return _messageReceiverCache.GetOrAdd(cacheKey,
                    new MessageReceiver(_options.Endpoint, entityPath, tokenProvider));
            }

            cacheKey = $"{entityPath}-{ConnectionString}";
            return _messageReceiverCache.GetOrAdd(cacheKey,
                new MessageReceiver(ConnectionString, entityPath)
                {
                    PrefetchCount = _options.PrefetchCount
                });
        }

        public MessageSender CreateMessageSender(string entityPath)
        {
            string cacheKey;
            if (_options.UseManagedServiceIdentity)
            {
                cacheKey = $"{entityPath}-{_options.Endpoint}";
                var tokenProvider = TokenProvider.CreateManagedServiceIdentityTokenProvider();
                return _messageSenderCache.GetOrAdd(cacheKey,
                    new MessageSender(_options.Endpoint, entityPath, tokenProvider));
            }
            else
            {
                cacheKey = $"{entityPath}-{ConnectionString}";
                return _messageSenderCache.GetOrAdd(cacheKey, new MessageSender(_options.ConnectionString, entityPath));
            }
        }


        public MessageReceiver CreateMessageReceiver(string entityPath)
        {
            string cacheKey;
            if (_options.UseManagedServiceIdentity)
            {
                cacheKey = $"{entityPath}-{_options.Endpoint}";
                var tokenProvider = TokenProvider.CreateManagedServiceIdentityTokenProvider();
                return _messageReceiverCache.GetOrAdd(cacheKey,
                    new MessageReceiver(_options.Endpoint, entityPath, tokenProvider)
                    {
                        PrefetchCount = _options.PrefetchCount
                    });
            }
            else
            {
                cacheKey = $"{entityPath}-{ConnectionString}";
                return _messageReceiverCache.GetOrAdd(cacheKey, new MessageReceiver(_options.ConnectionString, entityPath)
                {
                    PrefetchCount = _options.PrefetchCount
                });
            }
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
