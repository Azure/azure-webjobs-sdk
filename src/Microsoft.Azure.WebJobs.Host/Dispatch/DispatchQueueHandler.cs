// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Dispatch
{
    /// It is a wrapper around <see cref="SharedQueueHandler"/>
    internal class DispatchQueueHandler : IDispatchQueueHandler
    {
        private readonly SharedQueueHandler _sharedQueue;
        private readonly string _functionId;
        // the main purpose of this class is to encapsulate _functionId
        // so that when user try to enqueue function triggering message
        // they will only need to worry about the message content
        internal DispatchQueueHandler(SharedQueueHandler sharedQueue, string functionId)
        {
            _sharedQueue = sharedQueue;
            _functionId = functionId;
        }

        public Task EnqueueAsync(JObject message, CancellationToken cancellationToken)
        {
            return _sharedQueue.EnqueueAsync(message, _functionId, cancellationToken);
        }
    }
}
