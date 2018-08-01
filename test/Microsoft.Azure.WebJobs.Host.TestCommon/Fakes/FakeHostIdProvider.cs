// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public class FakeHostIdProvider : IHostIdProvider
    {
        private readonly string _hostId;

        public FakeHostIdProvider()
            : this(Guid.NewGuid().ToString("N"))
        {
        }

        public FakeHostIdProvider(string hostId)
        {
            _hostId = hostId;
        }

        public Task<string> GetHostIdAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_hostId);
        }
    }
}
