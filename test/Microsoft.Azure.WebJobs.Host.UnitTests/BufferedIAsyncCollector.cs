// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Xunit;


namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class BufferedAsyncCollectorTests
    {
        [Fact]
        public async Task T1()
        {
            var log = new StringBuilder();
            var inner = new TestCollector(log);
            var buffer = new BufferedIAsyncCollector<string>(inner);

            await buffer.AddAsync("1");
            await buffer.AddAsync("2");
            await buffer.AddAsync("3");

            // Nothing written yet. 
            Assert.Equal("", log.ToString());

            await buffer.FlushAsync();
            Assert.Equal("123[Flush]", log.ToString());

            // Now Adds are no longer buffered
            await buffer.AddAsync("4");
            Assert.Equal("123[Flush]4", log.ToString());

            // Extra flushes still call through.
            await buffer.FlushAsync();
            await buffer.FlushAsync();
            Assert.Equal("123[Flush]4[Flush][Flush]", log.ToString());
        }
        
        // Enough adds, will auto flush 
        [Fact]
        public async Task T2()
        {
            var log = new StringBuilder();
            var inner = new TestCollector(log);
            var buffer = new BufferedIAsyncCollector<string>(inner);

            const int N = BufferedIAsyncCollector<string>.Threshold;
            for (int i = 0; i < N - 1; i++)
            {
                await buffer.AddAsync(".");
            }
            Assert.Equal("", log.ToString()); // Not yet flushed!

            await buffer.AddAsync(".");

            var expected = new string('.', N);
            Assert.Equal(expected, log.ToString()); // Adds have been flushed to buffer. 

            // Writes after the initial flush are no longer buffered
            log.Clear();
            await buffer.AddAsync("x");
            Assert.Equal("x", log.ToString()); 
        }

        public class TestCollector : IAsyncCollector<string>
        {
            private readonly StringBuilder _log;

            public TestCollector(StringBuilder log)
            {
                _log = log;
            }

            public Task AddAsync(string item, CancellationToken cancellationToken = default(CancellationToken))
            {
                _log.Append(item);
                return Task.FromResult(0);
            }

            public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                _log.Append("[Flush]");
                return Task.FromResult(0);
            }
        }
    }


}
