using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Timers
{
    public class DefaultWebJobsExceptionHandlerFactory : IWebJobsExceptionHandlerFactory
    {
        public IWebJobsExceptionHandler Create(IHost jobHost) => new WebJobsExceptionHandler(jobHost);
    }
}
