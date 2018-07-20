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
        private readonly Func<Task> _postExecuteAction;

        public DelayedFunctionExecution(ITriggeredFunctionExecutor executor, TriggeredFunctionData data, Func<Task> postExecuteAction)
        {
            _executor = executor;
            _data = data;
            _postExecuteAction = postExecuteAction;
        }

        public async Task<FunctionResult> ExecuteAsync(CancellationToken token)
        {
            FunctionResult result = await _executor.TryExecuteAsync(_data, token);
            await _postExecuteAction.Invoke();
            return result;
        }
    }
}
