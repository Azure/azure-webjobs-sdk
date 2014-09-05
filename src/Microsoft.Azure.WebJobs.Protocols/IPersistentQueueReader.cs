﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if PUBLICPROTOCOL
namespace Microsoft.Azure.WebJobs.Protocols
#else
namespace Microsoft.Azure.WebJobs.Host.Protocols
#endif
{
    /// <summary>Defines a persistent queue reader.</summary>
    /// <typeparam name="T">The type of messages in the queue.</typeparam>
#if PUBLICPROTOCOL
    public interface IPersistentQueueReader<T>
#else
    internal interface IPersistentQueueReader<T>
#endif
    {
        /// <summary>Dequeues the next message in the queue, if any.</summary>
        /// <returns>The dequeued message, if any.</returns>
        /// <remarks>Dequeuing marks the message as temorarly invisible.</remarks>
        T Dequeue();

        /// <summary>Deletes a message from the queue.</summary>
        /// <param name="message">The message to delete.</param>
        void Delete(T message);

        /// <summary>Tries to makes an item's blob visible.</summary>
        /// <param name="message">The item to make visible.</param>
        void TryMakeItemVisible(T message);

        /// <summary>
        /// Gets the number of messages in the queue
        /// </summary>
        /// <param name="limit">Only counts up to a certain limit. If <see langword="null" />, counts all.</param>
        /// <returns>A positive value</returns>
        /// <remarks>Expensive operation when there are a lot of messages</remarks>
        int Count(int? limit);
    }
}
