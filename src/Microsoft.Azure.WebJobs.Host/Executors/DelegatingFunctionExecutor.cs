// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    abstract internal class DelegatingFunctionExecutor : IRetryNotifier, IFunctionExecutor
    {
        readonly protected IFunctionExecutor _innerExecutor;

        public DelegatingFunctionExecutor(IFunctionExecutor innerExecutor)
        {
            _innerExecutor = innerExecutor;
        }

        public virtual Task<IDelayedException> TryExecuteAsync(IFunctionInstance instance, CancellationToken cancellationToken)
        {
            return Task.FromResult<IDelayedException>(null);
        }

        public void RetryPending()
        {
            if (_innerExecutor is IRetryNotifier retryNotifier)
            {
                retryNotifier.RetryPending();
            }
        }

        public void RetryComplete()
        {
            if (_innerExecutor is IRetryNotifier retryNotifier)
            {
                retryNotifier.RetryComplete();
            }
        }
    }
}
