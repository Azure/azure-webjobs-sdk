// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Loggers;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class CompositeFunctionEventCollector : IAsyncCollector<FunctionInstanceLogEntry>, IDisposable
    {
        private readonly IEnumerable<IAsyncCollector<FunctionInstanceLogEntry>> _collectors;
        private bool _disposed = false;

        public CompositeFunctionEventCollector(params IAsyncCollector<FunctionInstanceLogEntry>[] collectors)
        {
            _collectors = collectors;
        }

        public async Task AddAsync(FunctionInstanceLogEntry item, CancellationToken cancellationToken = default(CancellationToken))
        {
            foreach (IAsyncCollector<FunctionInstanceLogEntry> collector in _collectors)
            {
                await collector.AddAsync(item, cancellationToken);
            }
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            foreach (IAsyncCollector<FunctionInstanceLogEntry> collector in _collectors)
            {
                await collector.FlushAsync(cancellationToken);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var collector in _collectors)
                {
                    (collector as IDisposable)?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
