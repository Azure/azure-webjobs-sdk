// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Config object for providing a container for distributed lock manager.
    /// This is hydrated from a <see cref="JobHostInternalStorageOptions"/>
    /// </summary>
    public class ManagedIdentityDistributedLockManagerContainerProvider : DistributedLockManagerContainerProvider
    {
        private readonly string storageAccountName;

        private readonly string tenantId;
        public ManagedIdentityDistributedLockManagerContainerProvider(string storageAccountName, string tenantId)
        {
            this.storageAccountName = storageAccountName;
            this.tenantId = tenantId;
        }

        public override CloudBlobContainer InternalContainer
        { 
            get
            {
                var tokenProvider = new AzureServiceTokenProvider();
                string accessToken = tokenProvider.GetAccessTokenAsync("https://storage.azure.com/", this.tenantId).Result;
                var storageCredentials = new StorageCredentials(new TokenCredential(accessToken));

                var cloudStorageAccount = new CloudStorageAccount(storageCredentials,
                    this.storageAccountName,
                    "core.windows.net",
                    true);

                var blobClient = cloudStorageAccount.CreateCloudBlobClient();
                var container = blobClient.GetContainerReference("azure-webjobs-hosts");

                return container;
            }
        }
    }
}
