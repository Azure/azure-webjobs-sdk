// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs.Host.Configuration
{
    internal class JobHostInternalStorageOptionsSetup : IConfigureOptions<JobHostInternalStorageOptions>
    {
        private IStorageAccountProvider _storageAccountProvider;

        public JobHostInternalStorageOptionsSetup(IStorageAccountProvider storageAccountProvider)
        {
            _storageAccountProvider = storageAccountProvider;
        }

        public void Configure(JobHostInternalStorageOptions options)
        {
            var sasBlobContainer = _storageAccountProvider.InternalSasStorage;
            if (sasBlobContainer != null)
            {
                var uri = new Uri(sasBlobContainer);
                var sdkContainer = new CloudBlobContainer(uri);

                options.InternalContainer = sdkContainer;
            }
        }
    }
}
