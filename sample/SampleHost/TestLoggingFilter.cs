using Microsoft.Azure.WebJobs;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Azure.WebJobs.Host;

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

        public override Task OnExecutingAsync(Object actionContext, CancellationToken cancellationToken)
        {
            Console.WriteLine(testMessage);
            return base.OnExecutingAsync(actionContext, cancellationToken);
        }

        public override Task OnActionExecuted(Object actionExecutedContext, CancellationToken cancellationToken)
        {
            Console.WriteLine(testMessage2);
            return base.OnActionExecuted(actionExecutedContext, cancellationToken);
        }
    }
}
