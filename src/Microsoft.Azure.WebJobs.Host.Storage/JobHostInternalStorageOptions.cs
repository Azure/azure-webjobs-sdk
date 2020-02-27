// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Options;
using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Config object for providing a container for distributed lock manager.
    /// This is hydrated from a <see cref="JobHostInternalStorageOptions"/>
    /// </summary>
    public class DistributedLockManagerContainerProvider
    {
        public DistributedLockManagerContainerProvider() { } 
        public DistributedLockManagerContainerProvider(IOptions<JobHostInternalStorageOptions> options)
        {
            SetContainerFromSharedAccessSignature(options);
            SetContainerFromAccessToken(options);
        }

        private void SetContainerFromSharedAccessSignature(IOptions<JobHostInternalStorageOptions> options)
        {
            var sasBlobContainer = options.Value.InternalSasBlobContainer;
            if (sasBlobContainer != null)
            {
                var uri = new Uri(sasBlobContainer);
                this.InternalContainer = new CloudBlobContainer(uri);
            }
        }

        private void SetContainerFromAccessToken(IOptions<JobHostInternalStorageOptions> options)
        {
            var tenantId = options.Value.TenantId;
            var accessToken = options.Value.AccessToken;
            var storageAccountName = options.Value.StorageAccountName;
            // Connect to storage using a Token that is specific to the defined Tenant from configuration
            if (!string.IsNullOrEmpty(tenantId))
            {
                if (string.IsNullOrEmpty(accessToken))
                {
                    // This code assumes that the running process has logged into Azure. It will not log in for you.
                    var tokenProvider = new AzureServiceTokenProvider();
                    accessToken = tokenProvider.GetAccessTokenAsync("https://storage.azure.com/", tenantId).Result;
                }
                var storageCredentials = new StorageCredentials(new TokenCredential(accessToken));

                var storageAccount = new CloudStorageAccount(storageCredentials,
                    storageAccountName,
                    "core.windows.net",
                    true);

                var blobClient = storageAccount.CreateCloudBlobClient();
                this.InternalContainer = blobClient.GetContainerReference(HostContainerNames.Hosts);
            }
        }

        /// <summary>
        /// A SAS to a Blob Container. This allows services to create blob leases and do distributed locking.
        /// If this is set, <see cref="JobHostConfiguration.StorageConnectionString"/> and 
        /// <see cref="JobHostConfiguration.DashboardConnectionString"/> can be set to null and the runtime will use the container.
        /// </summary>
        public CloudBlobContainer InternalContainer { get; set; }
    }

    /// <summary>
    /// The storage configuration that the JobHost needs for its own operations (independent of binding)
    /// For example, this can support <see cref="SingletonAttribute"/>, blob leases, timers, etc. 
    /// This provides a common place to set storage that the various subsequent services can use. 
    /// </summary>
    public class JobHostInternalStorageOptions
    {
        public string InternalSasBlobContainer { get; set; }
        /// <summary>
        /// This is used if you want to use a Storage Account for the host and you want to connect with Managed Identity
        /// </summary>
        public string StorageAccountName { get; set; }
        /// <summary>
        /// This is used if you want to use a Storage Account for the host and you want to connect with Managed Identity
        /// </summary>
        public string TenantId { get; set; }
        /// <summary>
        /// This is useful when debugging and you cannot login as the user that is running the process and you want
        /// to a use a Storage Account for the host and you want to connect with Managed Identity
        /// </summary>
        public string AccessToken { get; set; }
    }
}