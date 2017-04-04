// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Timers;

namespace Microsoft.Azure.WebJobs.Host.Triggers
{
    internal class FunctionExceptionHandler : IWebJobsExceptionHandler
    {
        private FunctionDescriptor _descriptor;
        private IWebJobsExceptionHandler _innerHandler;

        public FunctionExceptionHandler(FunctionDescriptor descriptor, IWebJobsExceptionHandler innerHandler)
        {
            _descriptor = descriptor;
            _innerHandler = innerHandler;
        }

        private ExceptionDispatchInfo Wrap(ExceptionDispatchInfo exceptionInfo)
        {
            var source = exceptionInfo.SourceException;
            var invocationException = new FunctionInvocationException(source.Message, Guid.Empty, _descriptor.ShortName, source);
            return ExceptionDispatchInfo.Capture(invocationException);
        }

        public void Initialize(JobHost host)
        {
            _innerHandler.Initialize(host);
        }

        public Task OnTimeoutExceptionAsync(ExceptionDispatchInfo exceptionInfo, TimeSpan timeoutGracePeriod)
        {
            return _innerHandler.OnTimeoutExceptionAsync(Wrap(exceptionInfo), timeoutGracePeriod);
        }

        public Task OnUnhandledExceptionAsync(ExceptionDispatchInfo exceptionInfo)
        {
            return _innerHandler.OnUnhandledExceptionAsync(Wrap(exceptionInfo));
        }
    }
}
