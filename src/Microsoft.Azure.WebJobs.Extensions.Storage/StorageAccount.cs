// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.Queue;

using CloudStorageAccount = Microsoft.Azure.Storage.CloudStorageAccount;
using StorageCredentials = Microsoft.Azure.Storage.Auth.StorageCredentials;
using TableStorageAccount = Microsoft.Azure.Cosmos.Table.CloudStorageAccount;

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
        public TableStorageAccount TableSdkObject { get; protected set; }

        /// <summary>
        /// Create a storage account from a connection string
        /// </summary>
        /// <param name="accountConnectionString"></param>
        /// <returns></returns>
        /// <remarks>This method is only ever called when we have a connection string</remarks>
        public static StorageAccount NewFromConnectionString(string accountConnectionString)
        {
            var account = CloudStorageAccount.Parse(accountConnectionString);
            var tableAccount = TableStorageAccount.Parse(accountConnectionString);
            return New(account, tableAccount, account.Credentials.AccountName);
        }

        internal static StorageAccount NewFromToken(string tenantId, string name, string accessToken = null)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentNullException(nameof(tenantId));
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                var tokenProvider = new AzureServiceTokenProvider();
                accessToken = tokenProvider.GetAccessTokenAsync("https://storage.azure.com/", tenantId).Result;
            }
            var storageCredentials = new StorageCredentials(new TokenCredential(accessToken));
            var cloudStorageAccount = new CloudStorageAccount(
                storageCredentials,
                name,
                "core.windows.net",
                true);

            // Create the cosmos table storage account - at the moment pass null??
            return New(cloudStorageAccount, null, name);
        }

        public static StorageAccount New(CloudStorageAccount account, TableStorageAccount tableAccount = null, string storageAccountName = null)
        {
            // The storage account name is normally in the credentials,
            // but not when we are using a managed identity
            if (storageAccountName == null)
            {
                storageAccountName = account.Credentials.AccountName;
            }

            return new StorageAccount
            { 
                SdkObject = account,
                TableSdkObject = tableAccount,
                Name = storageAccountName
            };
        }

        public virtual bool IsDevelopmentStorageAccount()
        {
            // see the section "Addressing local storage resources" in http://msdn.microsoft.com/en-us/library/windowsazure/hh403989.aspx
            return String.Equals(
                SdkObject.BlobEndpoint.PathAndQuery.TrimStart('/'),
                SdkObject.Credentials.AccountName,
                StringComparison.OrdinalIgnoreCase);
        }

        public virtual string Name { get; protected set; }

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
            return CloudStorageAccountExtensions.CreateCloudTableClient(TableSdkObject);            
        }
    }
}