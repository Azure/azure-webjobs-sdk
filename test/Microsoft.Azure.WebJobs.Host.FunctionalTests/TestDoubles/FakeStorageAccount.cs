﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.Azure.WebJobs.Host.Storage.Table;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests.TestDoubles
{
    internal class FakeStorageAccount : IStorageAccount
    {
        private static readonly StorageCredentials _credentials = new StorageCredentials("test", new byte[0]);
        private static readonly Uri _endpoint = new Uri("aa://b");

        private readonly MemoryBlobStore _blobStore = new MemoryBlobStore();
        private readonly MemoryQueueStore _queueStore = new MemoryQueueStore();
        private readonly MemoryTableStore _tableStore = new MemoryTableStore();
        private readonly CloudStorageAccount _sdkObject = new CloudStorageAccount(_credentials, _endpoint, _endpoint,
            _endpoint, _endpoint);
        private StorageAccountType _type = StorageAccountType.GeneralPurpose;

        public Uri BlobEndpoint
        {
            get { return _endpoint; }
        }

        public StorageCredentials Credentials
        {
            get { return _credentials; }
        }

        public static StorageCredentials DefaultCredentials
        {
            get { return _credentials; }
        }

        public CloudStorageAccount SdkObject
        {
            get { return _sdkObject; }
        }

        public StorageAccountType Type
        {
            get { return _type; }
            set { _type = value; }
        }

        public IStorageBlobClient CreateBlobClient(StorageClientFactoryContext context = null)
        {
            return new FakeStorageBlobClient(_blobStore, _credentials);
        }

        public IStorageQueueClient CreateQueueClient(StorageClientFactoryContext context = null)
        {
            return new FakeStorageQueueClient(_queueStore, _credentials);
        }

        public IStorageTableClient CreateTableClient(StorageClientFactoryContext context = null)
        {
            return new FakeStorageTableClient(_tableStore, _credentials);
        }

        public string ToString(bool exportSecrets)
        {
            throw new NotImplementedException();
        }
    }
}
