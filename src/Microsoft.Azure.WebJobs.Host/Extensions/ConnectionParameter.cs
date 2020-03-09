// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host
{
    public class ConnectionParameter : IUserTextElement
    {
        /// <summary>
        /// The type for the connection parameter.
        /// </summary>
        /// <remarks>Only value types are supported.</remarks>
        public Type Type { get; }

        /// <summary>
        /// Gets whether the connection parameter is required or not.
        /// </summary>
        public bool Required { get; }

        /// <inheritdoc/>
        public string Summary { get; }

        /// <inheritdoc/>
        public string Description { get; }

        /// <inheritdoc/>
        public string Tooltip { get; }
    }
}