﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.ServiceBus;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    /// <summary>
    /// Configuration options for the ServiceBus extension.
    /// </summary>
    public class ServiceBusConfiguration
    {
        private bool _connectionStringSet;
        private string _connectionString;
        private MessagingProvider _messagingProvider;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public ServiceBusConfiguration()
        {
            // Our default options will delegate to our own exception
            // logger. Customers can override this completely by setting their
            // own MessageHandlerOptions instance.
            MessageOptions = new MessageHandlerOptions(ExceptionReceivedHandler)
            {
                MaxConcurrentCalls = 16
            };
        }

        /// <summary>
        /// Gets or sets the Azure ServiceBus connection string.
        /// </summary>
        public string ConnectionString
        {
            get
            {
                if (!_connectionStringSet)
                {
                    _connectionString = AmbientConnectionStringProvider.Instance.GetConnectionString(ConnectionStringNames.ServiceBus);
                    _connectionStringSet = true;
                }

                return _connectionString;
            }
            set
            {
                _connectionString = value;
                _connectionStringSet = true;
            }
        }

        /// <summary>
        /// Gets or sets the default <see cref="MessageHandlerOptions"/> that will be used by
        /// <see cref="MessageReceiver"/>s.
        /// </summary>
        public MessageHandlerOptions MessageOptions { get; set; }

        /// <summary>
        /// Gets or sets the default PrefetchCount that will be used by <see cref="MessageReceiver"/>s.
        /// </summary>
        public int PrefetchCount { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="MessagingProvider"/> that will be used to create
        /// instances used for message processing.
        /// </summary>
        public MessagingProvider MessagingProvider
        {
            get
            {
                if (_messagingProvider == null)
                {
                    _messagingProvider = new MessagingProvider(this);
                }
                return _messagingProvider;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }
                _messagingProvider = value;
            }
        }

        internal Action<ExceptionReceivedEventArgs> ExceptionHandler { get; set; }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs args)
        {
            ExceptionHandler?.Invoke(args);

            return Task.CompletedTask;
        }
    }
}
