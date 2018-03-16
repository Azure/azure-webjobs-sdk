﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Storage;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests.TestDoubles
{
    internal class FakeStorageAccountProvider : IStorageAccountProvider
    {
        public IStorageAccount StorageAccount { get; set; }

        public IStorageAccount DashboardAccount { get; set; }

        public string StorageConnectionString => throw new System.NotImplementedException();

        public string DashboardConnectionString => throw new System.NotImplementedException();

        public Task<IStorageAccount> TryGetAccountAsync(string connectionStringName, CancellationToken cancellationToken)
        {
            IStorageAccount account;

            if (connectionStringName == ConnectionStringNames.Storage)
            {
                account = StorageAccount;
            }
            else if (connectionStringName == ConnectionStringNames.Dashboard)
            {
                account = DashboardAccount;
            }
            else
            {
                account = null;
            }

            return Task.FromResult(account);
        }
    }
}
