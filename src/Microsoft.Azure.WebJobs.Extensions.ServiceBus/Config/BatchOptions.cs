// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    /// <summary>
    /// Configuration options for ServiceBus batch receive.
    /// </summary>
    public class BatchOptions
    {
        /// <summary>
        /// The maximum number of messages that will be received.
        /// </summary>
        public int MaxMessageCount { get; set; }

        /// <summary>
        /// The time span the client waits for receiving a message before it times out.
        /// </summary>
        public TimeSpan OperationTimeout { get; set; }

        /// <summary>
        /// The time span client waits between receiving attempts.
        /// </summary>
        public TimeSpan DelayBetweenOperations { get; set; }
    }
}
