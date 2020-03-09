// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Metadata for function Binding connection
    /// </summary>
    public class ConnectionMetadata
    {
        public IEnumerable<ConnectionParameter> ConnectionParameters { get;  }
    }
}