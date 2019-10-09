// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Provide a storage account from a Managed Service Identity
    /// </summary>
    public class ManagedIdentityStorageAccountProvider : StorageAccountProvider
    {
        private readonly StorageCredentials storageCredentials;

        /// <summary>
        /// The tenantId is used when we create this object so that the token is scoped to the
        /// correct Azure AD tenant.
        /// </summary>
        public ManagedIdentityStorageAccountProvider(string tenantId) : base(null)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentNullException(nameof(tenantId));
            }

            var tokenProvider = new AzureServiceTokenProvider();
            string accessToken = tokenProvider.GetAccessTokenAsync("https://storage.azure.com/", tenantId).Result;
            this.storageCredentials = new StorageCredentials(new TokenCredential(accessToken));
        }

        /// <summary>
        /// Override default behaviour to return a CloudStorageAccount using Managed Identity.
        /// The name of the Storage Account is passed in via the <see cref="StorageAccountAttribute"/>.
        /// </summary>
        public override StorageAccount Get(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
            var cloudStorageAccount = new CloudStorageAccount(
                this.storageCredentials,
                name,
                "core.windows.net",
                true);

            return StorageAccount.New(cloudStorageAccount);
        }

        /// <summary>
        /// Override default behaviour to return a CloudStorageAccount using Managed Identity.
        /// The name of the Storage Account is passed in via the <see cref="StorageAccountAttribute"/>.
        /// </summary>
        public override StorageAccount GetHost(string internalStorageName)
        {
            // Managed Identity has a connection to the Storage Account
            return this.Get(internalStorageName);
        }
    }
}