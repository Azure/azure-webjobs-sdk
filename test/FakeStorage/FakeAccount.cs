// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FakeStorage
{
    public class FakeAccount
    {
        internal StorageCredentials _creds;

        internal readonly MemoryBlobStore _blobStore = new MemoryBlobStore();
        internal readonly MemoryTableStore Store = new MemoryTableStore();

        public CloudTableClient CreateCloudTableClient()
        {
            return new FakeTableClient(this);
        }

        public CloudBlobClient CreateCloudBlobClient()
        {
            return new FakeStorageBlobClient(this);
        }

        // For testing, set a blob instance. 
        public void SetBlob(string containerName, string blobName, CloudBlockBlob blob)
        {
            _blobStore.SetBlob(containerName, blobName, blob);
        }
    }
}