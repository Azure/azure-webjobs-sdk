// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    /// <summary>
    /// The TaskWrapper template type.
    /// </summary>
    internal class EventHubTaskWrapper
    {
        /// <summary>
        /// The Task Wrapper.
        /// </summary>
        /// <param name="workItem"></param>
        public EventHubTaskWrapper(TriggeredFunctionData workItem)
        {
            this.WorkItem = workItem;
            this.CompletionSource = new TaskCompletionSource<int>();
        }

        /// <summary>
        /// The TaskCompletionSource.
        /// </summary>
        public TaskCompletionSource<int> CompletionSource { get; private set; }

        /// <summary>
        /// The work item.
        /// </summary>
        public TriggeredFunctionData WorkItem { get; private set; }
    }
}