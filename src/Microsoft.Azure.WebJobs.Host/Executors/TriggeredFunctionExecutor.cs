// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class TriggeredFunctionExecutor<TTriggerValue> : ITriggeredFunctionExecutor
    {
        private readonly ILoggerFactory _loggerFactory;
        private FunctionDescriptor _descriptor;
        private ITriggeredFunctionInstanceFactory<TTriggerValue> _instanceFactory;
        private IFunctionExecutor _executor;

        public TriggeredFunctionExecutor(FunctionDescriptor descriptor, IFunctionExecutor executor, ITriggeredFunctionInstanceFactory<TTriggerValue> instanceFactory, ILoggerFactory loggerFactory)
        {
            _descriptor = descriptor;
            _executor = executor;
            _instanceFactory = instanceFactory;
            _loggerFactory = loggerFactory;
        }

        public FunctionDescriptor Function
        {
            get
            {
                return _descriptor;
            }
        }

        public async Task<FunctionResult> TryExecuteAsync(TriggeredFunctionData input, CancellationToken cancellationToken)
        {
            var context = new FunctionInstanceFactoryContext<TTriggerValue>()
            {
                TriggerValue = (TTriggerValue)input.TriggerValue,
                ParentId = input.ParentId,
                TriggerDetails = input.TriggerDetails
            };

            if (input.InvokeHandler != null)
            {
                context.InvokeHandler = async next =>
                {
                    await input.InvokeHandler(next);

                    // NOTE: The InvokeHandler code path currently does not support flowing the return 
                    // value back to the trigger.
                    return null;
                };
            }

            Func<IFunctionInstance> instanceFactory = () => _instanceFactory.Create(context);
            IDelayedException exception = await _executor.TryExecuteAsync(instanceFactory, _loggerFactory, cancellationToken);

            FunctionResult result = exception != null ?
                new FunctionResult(exception.Exception)
                : new FunctionResult(true);

            return result;
        }
    }
}
