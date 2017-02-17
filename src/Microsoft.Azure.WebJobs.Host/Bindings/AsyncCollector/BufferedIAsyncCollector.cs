// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// Buffer up to the first N items. 
    /// If Flush() is called, stop buffering. 
    /// This just buffers Add() calls, it does not inject any additional calls to Flush(). 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class BufferedIAsyncCollector<T> : IAsyncCollector<T>
    {
        public const int Threshold = 1000;

        private readonly IAsyncCollector<T> _inner;

        private List<T> _buffer = new List<T>();

        public BufferedIAsyncCollector(IAsyncCollector<T> inner)
        {
            _inner = inner;
        }

        public async Task AddAsync(T item, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_buffer == null)
            {
                // Buffering is disabled.
                await _inner.AddAsync(item, cancellationToken);
                return;
            }
            else
            {
                _buffer.Add(item);
                if (_buffer.Count == Threshold)
                {
                    await DrainBufferAsync(cancellationToken);
                }
            }
        }

        // Drain the calls to Add(), but don't call Flush(). 
        private async Task DrainBufferAsync(CancellationToken cancellationToken)
        {
            if (_buffer != null)
            {
                foreach (var item in _buffer)
                {
                    cancellationToken.ThrowIfCancellationRequested(); // break the loop. 
                    await this._inner.AddAsync(item, cancellationToken);
                }
                _buffer = null;
            }
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await DrainBufferAsync(cancellationToken);
            await _inner.FlushAsync(cancellationToken);
        }
    }
}
