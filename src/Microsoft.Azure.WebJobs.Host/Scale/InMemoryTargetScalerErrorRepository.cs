// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// In-memory implementation of <see cref="ITargetScalerErrorRepository"/>.
    /// This is used when no persistent storage is configured. State is only visible
    /// within the current process and will not be shared across workers.
    /// </summary>
    internal class InMemoryTargetScalerErrorRepository : ITargetScalerErrorRepository
    {
        private readonly ConcurrentDictionary<string, byte> _scalersInError = new ConcurrentDictionary<string, byte>();

        public Task AddAsync(string scalerUniqueId, CancellationToken cancellationToken)
        {
            _scalersInError.TryAdd(scalerUniqueId, 0);
            return Task.CompletedTask;
        }

        public Task<ISet<string>> GetAsync(CancellationToken cancellationToken)
        {
            ISet<string> result = new HashSet<string>(_scalersInError.Keys);
            return Task.FromResult(result);
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            _scalersInError.Clear();
            return Task.CompletedTask;
        }
    }
}
