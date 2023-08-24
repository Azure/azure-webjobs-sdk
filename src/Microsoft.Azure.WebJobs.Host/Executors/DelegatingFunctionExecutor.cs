// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal abstract class DelegatingFunctionExecutor : IRetryNotifier, IFunctionExecutor
    {
        private readonly IFunctionExecutor _innerExecutor;
        private readonly IRetryNotifier _retryNotifier;

        public DelegatingFunctionExecutor(IFunctionExecutor innerExecutor)
        {
            _innerExecutor = innerExecutor;
            _retryNotifier = innerExecutor as IRetryNotifier;
        }

        public virtual Task<IDelayedException> TryExecuteAsync(IFunctionInstance instance, CancellationToken cancellationToken)
        {
            return _innerExecutor.TryExecuteAsync(instance, cancellationToken);
        }

        public virtual void RetryPending()
        {
            _retryNotifier?.RetryPending();
        }

        public virtual void RetryComplete()
        {
            _retryNotifier?.RetryComplete();
        }
    }
}
