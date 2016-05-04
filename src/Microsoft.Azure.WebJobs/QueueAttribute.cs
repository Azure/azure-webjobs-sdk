﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to bind a parameter to an Azure Queue.
    /// </summary>
    /// <remarks>
    /// The method parameter type can be one of the following:
    /// <list type="bullet">
    /// <item><description>CloudQueue</description></item>
    /// <item><description>CloudQueueMessage (out parameter)</description></item>
    /// <item><description><see cref="string"/> (out parameter)</description></item>
    /// <item><description><see cref="T:byte[]"/> (out parameter)</description></item>
    /// <item><description>A user-defined type (out parameter, serialized as JSON)</description></item>
    /// <item><description><see cref="ICollector{T}"/> of these types (to enqueue multiple messages via <see cref="ICollector{T}.Add"/></description></item>
    /// <item><description><see cref="IAsyncCollector{T}"/> of these types (to enqueue multiple messages via <see cref="IAsyncCollector{T}.AddAsync(T, System.Threading.CancellationToken)"/></description></item>
    /// </list>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1813:AvoidUnsealedAttributes")]
    [AttributeUsage(AttributeTargets.Parameter)]
    [DebuggerDisplay("{QueueName,nq}")]
    public class QueueAttribute : Attribute
    {
        private readonly string _queueName;

        /// <summary>Initializes a new instance of the <see cref="QueueAttribute"/> class.</summary>
        /// <param name="queueName">The name of the queue to which to bind.</param>
        public QueueAttribute(string queueName)
        {
            _queueName = queueName;
        }

        /// <summary>
        /// Gets the name of the queue to which to bind.
        /// </summary>
        [AutoResolve]
        public string QueueName
        {
            get { return _queueName; }
        }      
    }
}
