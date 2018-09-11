// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    /// <summary>
    /// Configuration options for the ServiceBus extension.
    /// </summary>
    public class ServiceBusOptions
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public ServiceBusOptions()
        {
            // Our default options will delegate to our own exception
            // logger. Customers can override this completely by setting their
            // own MessageHandlerOptions instance.
            MessageHandlerOptions = new MessageHandlerOptions(ExceptionReceivedHandler)
            {
                MaxConcurrentCalls = 16
            };
        }

        /// <summary>
        /// Gets or sets the Azure ServiceBus connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the default <see cref="Azure.ServiceBus.MessageHandlerOptions"/> that will be used by
        /// <see cref="MessageReceiver"/>s.
        /// </summary>
        public MessageHandlerOptions MessageHandlerOptions { get; set; }

        /// <summary>
        /// Gets or sets the default PrefetchCount that will be used by <see cref="MessageReceiver"/>s.
        /// </summary>
        public int PrefetchCount { get; set; }

        internal Action<ExceptionReceivedEventArgs> ExceptionHandler { get; set; }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs args)
        {
            ExceptionHandler?.Invoke(args);

            return Task.CompletedTask;
        }
    }
}
