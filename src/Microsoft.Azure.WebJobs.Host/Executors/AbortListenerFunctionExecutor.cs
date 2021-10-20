// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class AbortListenerFunctionExecutor : DelegatingFunctionExecutor
    {
        private readonly IListenerFactory _abortListenerFactory;

        public AbortListenerFunctionExecutor(IListenerFactory abortListenerFactory, IFunctionExecutor innerExecutor) : base(innerExecutor)
        {
            _abortListenerFactory = abortListenerFactory;
        }

        public override async Task<IDelayedException> TryExecuteAsync(IFunctionInstance instance, CancellationToken cancellationToken)
        {
            IDelayedException result;

            using (IListener listener = await _abortListenerFactory.CreateAsync(cancellationToken))
            {
                await listener.StartAsync(cancellationToken);

                result = await base.TryExecuteAsync(instance, cancellationToken);

                await listener.StopAsync(cancellationToken);
            }

            return result;
        }
    }
}
