// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    /// <summary>
    /// Represents the input values for a triggered function invocation.
    /// </summary>
    public class TriggeredFunctionData
    {
        private IDictionary<string, string> _triggerDetails;

        /// <summary>
        /// The parent ID for the triggered function invocation.
        /// </summary>
        public Guid? ParentId { get; set; }

        /// <summary>
        /// The trigger value for a specific triggered function invocation.
        /// </summary>
        public object TriggerValue { get; set; }

        /// <summary>
        /// Details of the trigger (e.g. Message ID, insertion time, dequeue count etc.)
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public IDictionary<string, string> TriggerDetails { get => _triggerDetails; set => _triggerDetails = value; }

        /// <summary>
        /// Optional handler function for processing the invocation.
        /// </summary>
        [Obsolete("Not ready for public consumption.")]
        public Func<Func<Task>, Task> InvokeHandler { get; set; }
    }
}
