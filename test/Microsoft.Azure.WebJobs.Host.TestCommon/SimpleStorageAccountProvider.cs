// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public class SimpleStorageAccountProvider : IStorageAccountProvider
    {
        private readonly StorageClientFactory _storageClientFactory;

        public SimpleStorageAccountProvider(StorageClientFactory storageClientFactory)
        {
            _storageClientFactory = storageClientFactory;
        }

        public CloudStorageAccount StorageAccount { get; set; }

        public CloudStorageAccount DashboardAccount { get; set; }

        public string StorageConnectionString => null;

        public string DashboardConnectionString => null;

        public string InternalSasStorage => null;

        Task<IStorageAccount> IStorageAccountProvider.TryGetAccountAsync(string connectionStringName, CancellationToken cancellationToken)
        {
            IStorageAccount account;

            if (connectionStringName == ConnectionStringNames.Dashboard)
            {
                account = DashboardAccount != null ? new StorageAccount(DashboardAccount, _storageClientFactory) : null;
            }
            else if (connectionStringName == ConnectionStringNames.Storage)
            {
                account = StorageAccount != null ? new StorageAccount(StorageAccount, _storageClientFactory) : null;
            }
            else
            {
                account = null;
            }

            return Task.FromResult(account);
        }
    }
}
