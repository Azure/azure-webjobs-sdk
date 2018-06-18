// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs
{
    // Storage Extension's abstraction for a storage provider. 
    // $$$ Replaced DefaultStorageProvider, IStorageAccountProvider.
    public class XStorageAccountProvider
    {
        private readonly IConnectionStringProvider _provider;

        public XStorageAccountProvider(IConnectionStringProvider provider)
        {
            _provider = provider;
        }

        // $$$ Where does validation happen? 
        // $$$ Does this guy call NameResolver? Or his Caller? 
        public XStorageAccount Get(string name, INameResolver resolver)
        {
            var resolvedName = resolver.ResolveWholeString(name);
            return this.Get(resolvedName);
        }

        public virtual XStorageAccount Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Storage"; // default
            }

            // Connection string provider? 
            string cx = _provider.GetConnectionString(name);
            if (cx == null)
            {
                // Not found
            }

            return XStorageAccount.NewFromConnectionString(cx);
        }

        public virtual XStorageAccount GetHost()
        {
            return this.Get(null); // $$$ default name
        }
    }

    // Wrapper for unit testing.
    // CloudStorageAccount is not virtual, but all the other classes are. 
    public class XStorageAccount
    {
        protected CloudStorageAccount _account;

        // $$$ Can we get rid of this? It breaks the abstraction
        public CloudStorageAccount SdkObject => _account;

        public static XStorageAccount NewFromConnectionString(string accountConnectionString)
        {
            var account = CloudStorageAccount.Parse(accountConnectionString);
            return New(account);
        }

        public static XStorageAccount New(CloudStorageAccount account)
        {
            return new XStorageAccount { _account = account };
        }

        public virtual bool IsDevelopmentStorageAccount()
        {
            // see the section "Addressing local storage resources" in http://msdn.microsoft.com/en-us/library/windowsazure/hh403989.aspx
            return String.Equals(
                _account.BlobEndpoint.PathAndQuery.TrimStart('/'),
                _account.Credentials.AccountName,
                StringComparison.OrdinalIgnoreCase);
        }

        public virtual string Name
        {
            get { return _account.Credentials.AccountName; }
        }

        public virtual Uri BlobEndpoint => _account.BlobEndpoint;

        public virtual CloudBlobClient CreateCloudBlobClient()
        {
            return _account.CreateCloudBlobClient();
        }
        public virtual CloudQueueClient CreateCloudQueueClient()
        {
            return _account.CreateCloudQueueClient();
        }

        public virtual CloudTableClient CreateCloudTableClient()
        {
            return _account.CreateCloudTableClient();
        }
    }
}