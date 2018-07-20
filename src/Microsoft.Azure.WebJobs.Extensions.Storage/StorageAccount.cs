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

    /// <summary>
    /// Wrapper around a CloudStorageAccount for abstractions and unit testing. 
    /// This is handed out by <see cref="StorageAccountProvider"/>.
    /// CloudStorageAccount is not virtual, but all the other classes below it are. 
    /// </summary>
    public class StorageAccount
    {
        /// <summary>
        /// Get the real azure storage account. Only use this if you explicitly need to bind to the <see cref="CloudStorageAccount"/>, 
        /// else use the virtuals. 
        /// </summary>
        public CloudStorageAccount SdkObject { get; protected set; }

        public static StorageAccount NewFromConnectionString(string accountConnectionString)
        {
            var account = CloudStorageAccount.Parse(accountConnectionString);
            return New(account);
        }

        public static StorageAccount New(CloudStorageAccount account)
        {
            return new StorageAccount { SdkObject = account };
        }

        public virtual bool IsDevelopmentStorageAccount()
        {
            // see the section "Addressing local storage resources" in http://msdn.microsoft.com/en-us/library/windowsazure/hh403989.aspx
            return String.Equals(
                SdkObject.BlobEndpoint.PathAndQuery.TrimStart('/'),
                SdkObject.Credentials.AccountName,
                StringComparison.OrdinalIgnoreCase);
        }

        public virtual string Name
        {
            get { return SdkObject.Credentials.AccountName; }
        }

        public virtual Uri BlobEndpoint => SdkObject.BlobEndpoint;

        public virtual CloudBlobClient CreateCloudBlobClient()
        {
            return SdkObject.CreateCloudBlobClient();
        }
        public virtual CloudQueueClient CreateCloudQueueClient()
        {
            return SdkObject.CreateCloudQueueClient();
        }

        public virtual CloudTableClient CreateCloudTableClient()
        {
            return SdkObject.CreateCloudTableClient();
        }
    }
}