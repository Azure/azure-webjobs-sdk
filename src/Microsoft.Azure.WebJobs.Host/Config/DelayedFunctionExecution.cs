using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    public class DelayedFunctionExecution
    {
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly TriggeredFunctionData _data;

        public DelayedFunctionExecution(ITriggeredFunctionExecutor executor, TriggeredFunctionData data)
        {
            _executor = executor;
            _data = data;
        }

        public async Task<FunctionResult> ExecuteAsync(CancellationToken token)
        {
            return await _executor.TryExecuteAsync(_data, token);
        }
    }
}
