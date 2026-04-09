// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    internal class NullTargetScalerErrorRepository : ITargetScalerErrorRepository
    {
        public Task AddAsync(string scalerUniqueId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<ISet<string>> GetAsync(CancellationToken cancellationToken)
        {
            ISet<string> result = new HashSet<string>();
            return Task.FromResult(result);
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
