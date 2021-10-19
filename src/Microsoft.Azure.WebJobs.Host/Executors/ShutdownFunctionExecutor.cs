// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class ShutdownFunctionExecutor : DelegatingFunctionExecutor
    {
        private readonly CancellationToken _shutdownToken;

        public ShutdownFunctionExecutor(CancellationToken shutdownToken, IFunctionExecutor innerExecutor) : base(innerExecutor)
        {
            _shutdownToken = shutdownToken;
        }

        public override async Task<IDelayedException> TryExecuteAsync(IFunctionInstance instance, CancellationToken cancellationToken)
        {
            using (CancellationTokenSource callCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                _shutdownToken, cancellationToken))
            {
                return await base.TryExecuteAsync(instance, callCancellationSource.Token);
            }
        }
    }
}
