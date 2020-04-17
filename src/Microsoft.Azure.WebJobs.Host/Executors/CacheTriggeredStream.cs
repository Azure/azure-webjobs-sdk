// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    public class CacheTriggeredStream
    {
        public CacheTriggeredStream(Stream stream, CacheObjectMetadata metadata)
        {
            Stream = stream;
            Metadata = metadata;
        }
        
        public Stream Stream { get; private set; }
        public CacheObjectMetadata Metadata { get; private set; }
    }
}
