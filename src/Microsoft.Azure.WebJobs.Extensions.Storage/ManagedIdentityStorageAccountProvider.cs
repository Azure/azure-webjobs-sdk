// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Provide storage account from a Managed Service Identity
    /// </summary>
    public class ManagedIdentityStorageAccountProvider : StorageAccountProvider
    {
        private readonly StorageCredentials storageCredentials;
        public ManagedIdentityStorageAccountProvider() : base(null)
        {
            var tokenProvider = new AzureServiceTokenProvider();
            string accessToken = tokenProvider.GetAccessTokenAsync("https://storage.azure.com/", "2fa2ec5a-717a-4157-8e6c-f3ec61fed660").Result;
            this.storageCredentials = new StorageCredentials(new TokenCredential(accessToken));
        }

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

        public override StorageAccount GetHost(string internalStorageName)
        {
            // Managed Identity has a connection to the Storage Account
            return this.Get(internalStorageName);
        }
    }
}