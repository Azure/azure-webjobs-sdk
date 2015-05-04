// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class TriggeredFunctionExecutor<TTriggerValue> : ITriggeredFunctionExecutor
    {
        private FunctionDescription _description;
        private ITriggeredFunctionInstanceFactory<TTriggerValue> _instanceFactory;
        private IFunctionExecutor _executor;

        public TriggeredFunctionExecutor(FunctionDescriptor descriptor, IFunctionExecutor executor, ITriggeredFunctionInstanceFactory<TTriggerValue> instanceFactory)
        {
            _description = new FunctionDescription
            {
                ID = descriptor.Id,
                FullName = descriptor.FullName
            };
            _executor = executor;
            _instanceFactory = instanceFactory;
        }

        public FunctionDescription Function
        {
            get
            {
                return _description;
            }
        }

        public async Task<bool> TryExecuteAsync(Guid? parentId, object triggerValue, CancellationToken cancellationToken)
        {
            IFunctionInstance instance = _instanceFactory.Create((TTriggerValue)triggerValue, parentId);
            IDelayedException exception = await _executor.TryExecuteAsync(instance, cancellationToken);
            return exception == null;
        }
    }
}
