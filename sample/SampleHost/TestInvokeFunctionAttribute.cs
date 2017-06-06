using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace SampleHost
{
    public class TestInvokeFunctionAttribute : InvocationFilterAttribute
    {
        public string functionNameToInvoke;

        public TestInvokeFunctionAttribute(string functionNameToInvoke)
        {
            this.functionNameToInvoke = functionNameToInvoke;
        }

        public override Task OnPreFunctionInvocation(FunctionExecutingContext actionContext, CancellationToken cancellationToken)
        {

            return base.OnPreFunctionInvocation(actionContext, cancellationToken);
        }
    }
}