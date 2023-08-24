// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Attribute applied to a <see cref="ITriggerBinding"/> implementation when the binding uses
    /// a shared listener for all functions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SharedListenerAttribute : Attribute
    {
        public SharedListenerAttribute(string sharedListenerId)
        {
            SharedListenerId = sharedListenerId;
        }

        /// <summary>
        /// Gets the shared ID used for all functions.
        /// </summary>
        public string SharedListenerId { get; }
    }
}
