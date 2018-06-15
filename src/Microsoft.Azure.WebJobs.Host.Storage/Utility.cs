// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs
{
    static class Utility
    {
        // CloudBlobDirectory has a private ctor, so we can't actually override it. 
        // This overload is unit-testable 
        internal static CloudBlockBlob SafeGetBlockBlobReference(this CloudBlobDirectory dir, string blobName)
        {
            var container = dir.Container;
            var prefix = dir.Prefix; // already ends in /
            var blob = container.GetBlockBlobReference(prefix + blobName);
            return blob;
        }
    }
}
