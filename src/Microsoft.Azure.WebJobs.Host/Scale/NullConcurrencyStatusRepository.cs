// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    internal class NullConcurrencyStatusRepository : IConcurrencyStatusRepository
    {
        public Task<HostConcurrencySnapshot?> ReadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<HostConcurrencySnapshot?>(null);
        }

        public Task WriteAsync(HostConcurrencySnapshot snapshot, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
