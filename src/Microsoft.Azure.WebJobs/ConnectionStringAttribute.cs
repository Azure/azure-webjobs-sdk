// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Description
{
    /// <summary>
    /// Place this on binding attributes properties to tell the binders that that the property
    /// should be automatically resolved as a connection string.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ConnectionStringAttribute : Attribute
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public ConnectionStringAttribute()
        {
        }

        /// <summary>
        /// The default connection string name to use, if none specified
        /// </summary>
        public string Default { get; set; }
    }
}