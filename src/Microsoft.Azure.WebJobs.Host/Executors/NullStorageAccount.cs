// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

#if PUBLICSTORAGE
using Microsoft.Azure.WebJobs.Storage;
using Microsoft.Azure.WebJobs.Storage.Blob;
using Microsoft.Azure.WebJobs.Storage.Queue;
using Microsoft.Azure.WebJobs.Storage.Table;
#else
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.Azure.WebJobs.Host.Storage.Table;
#endif
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class NullStorageAccount : IStorageAccount
    {
        private readonly string _errorMessage;

        public NullStorageAccount(string errorMessage)
        {
            _errorMessage = errorMessage;
        }

        public StorageCredentials Credentials
        {
            get { return null; }
        }

        public CloudStorageAccount SdkObject
        {
            get { return null; }
        }

        public IStorageBlobClient CreateBlobClient()
        {
            throw new InvalidOperationException(_errorMessage);
        }

        public IStorageQueueClient CreateQueueClient()
        {
            throw new InvalidOperationException(_errorMessage);
        }

        public IStorageTableClient CreateTableClient()
        {
            throw new InvalidOperationException(_errorMessage);
        }

        public string ToString(bool exportSecrets)
        {
            throw new InvalidOperationException(_errorMessage);
        }
    }
}
