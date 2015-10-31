// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Storage;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal interface IStorageAccountProvider
    {
        IStorageAccount GetAccountFromConnectionString(string connectionString);

        Task<IStorageAccount> GetAccountAsync(string connectionStringName, CancellationToken cancellationToken);
    }
}
