using Microsoft.Azure.WebJobs;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace SampleHost
{
    public class TestInvokeFunctionAttribute : InvocationFilterAttribute
    {
        public string functionNameToInvoke;

        public TestInvokeFunctionAttribute(string functionNameToInvoke)
        {
            this.functionNameToInvoke = functionNameToInvoke;
        }

        public override Task OnExecutingAsync(FunctionExecutedContext actionContext, CancellationToken cancellationToken)
        {

            return base.OnExecutingAsync(actionContext, cancellationToken);
        }
    }
}