// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    /// <summary>
    /// The message state.
    /// </summary>
    internal enum State
    {
        /// <summary>
        /// Running state.
        /// </summary>
        Running,

        /// <summary>
        /// Complete state.
        /// </summary>
        Complete,

        /// <summary>
        /// Faulted state.
        /// </summary>
        Faulted
    }

    /// <summary>
    /// The IMessageStatusManager interface.
    /// </summary>
    internal interface IMessageStatusManager
    {
        /// <summary>
        /// The active task count.
        /// </summary>
        long ActiveTaskCount { get; }

        /// <summary>
        /// Set task as running
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="timeToLive"></param>
        /// <param name="elapsed"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        Task SetRunning(Guid messageId, TimeSpan timeToLive, TimeSpan elapsed,
            byte[] context);

        /// <summary>
        /// Set task as completed.
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="elapsed"></param>
        /// <returns></returns>
        Task SetComplete(Guid messageId, TimeSpan elapsed);
    }
}