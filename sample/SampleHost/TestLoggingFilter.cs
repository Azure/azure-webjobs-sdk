using Microsoft.Azure.WebJobs;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace SampleHost
{
    public class TestLoggingFilter : InvocationFilterAttribute
    {
        public string testMessage;

        public TestLoggingFilter(string testMessage)
        {
            this.testMessage = testMessage;
        }

        public override Task OnExecutingAsync(FunctionExecutedContext actionContext, CancellationToken cancellationToken)
        {
            Console.WriteLine("Before: " + testMessage);
            return base.OnExecutingAsync(actionContext, cancellationToken);
        }

        public override Task OnActionExecuted(FunctionExecutedContext actionExecutedContext, CancellationToken cancellationToken)
        {
            Console.WriteLine("After: " + testMessage);
            return base.OnActionExecuted(actionExecutedContext, cancellationToken);
        }
    }
}
