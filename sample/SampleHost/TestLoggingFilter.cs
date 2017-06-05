using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace SampleHost
{
    public class TestLoggingFilter : InvocationFilterAttribute
    {
        public string testMessage;
        public string testMessage2;

        public TestLoggingFilter(string testMessage, string testMessage2)
        {
            this.testMessage = testMessage;
            this.testMessage2 = testMessage2;
        }

        public override Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
        {
            Console.WriteLine(testMessage);
            return base.OnExecutingAsync(executingContext, cancellationToken);
        }

        public override Task OnActionExecuted(FunctionExecutedContext executedContext, CancellationToken cancellationToken)
        {
            Console.WriteLine(testMessage2);
            return base.OnActionExecuted(executedContext, cancellationToken);
        }
    }
}
