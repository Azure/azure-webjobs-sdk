// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Executors
{

    // This class represents triggers that happened from the cache and holds relevant information
    public class CacheTriggeredInput
    {
        public CacheTriggeredInput(CacheObjectMetadata metadata)
        {
            Metadata = metadata;
        }
        
        public CacheObjectMetadata Metadata { get; private set; }
    }
}
