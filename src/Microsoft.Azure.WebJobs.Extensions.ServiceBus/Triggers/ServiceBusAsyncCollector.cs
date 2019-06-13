// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace Microsoft.Azure.WebJobs.ServiceBus.Bindings
{
    /// <summary>
    /// Core object to send events to ServiceBus. 
    /// Any user parameter that sends ServiceBus messages will eventually get bound to this object. 
    /// Batching is not supported.
    /// </summary>
    internal class ServiceBusAsyncCollector : IAsyncCollector<Message>
    {
        private MessageSender _messageSender { get; set; }

        public ServiceBusAsyncCollector(MessageSender sender)
        {
            _messageSender = sender;
        }

        /// <summary>
        /// Add a message. 
        /// </summary>
        /// <param name="item">The message to add</param>
        /// <param name="cancellationToken">a cancellation token. </param>
        /// <returns></returns>
        public async Task AddAsync(Message item, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (item == null)
            {
                throw new InvalidOperationException("Cannot enqueue a null message instance.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            await _messageSender.SendAsync(item);
        }

        /// <summary>
        /// synchronously flush messages that have been queued up via AddAsync.
        /// </summary>
        /// <param name="cancellationToken">a cancellation token</param>
        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // Batching is not supported. 
            return Task.FromResult(0);
        }
    }
}
