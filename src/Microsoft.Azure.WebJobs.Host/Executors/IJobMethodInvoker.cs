using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal interface IJobMethodInvoker
    {
        Task JobInvokeAsync(string method, IDictionary<string, object> parameters, CancellationToken cancellationToken);
    }
}