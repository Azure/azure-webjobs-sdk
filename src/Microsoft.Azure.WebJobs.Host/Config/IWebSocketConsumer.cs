using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    public interface IWebSocketConsumer
    {
        Task<DelayedFunctionExecution> GetFunctionExecutionAsync(WebSocket socket, CancellationToken token);
    }
}
