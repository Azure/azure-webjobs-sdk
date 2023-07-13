// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Listeners
{
    internal class FunctionListenerDecorator : IListenerDecorator
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOptions<JobHostOptions> _jobHostOptions;

        public FunctionListenerDecorator(ILoggerFactory loggerFactory, IOptions<JobHostOptions> jobHostOptions)
        {
            _loggerFactory = loggerFactory;
            _jobHostOptions = jobHostOptions;
        }

        public IListener Decorate(ListenerDecoratorContext context)
        {
            // wrap the listener with a function listener to handle exceptions
            bool allowPartialHostStartup = _jobHostOptions.Value.AllowPartialHostStartup;
            return new FunctionListener(context.Listener, context.FunctionDefinition.Descriptor, _loggerFactory, allowPartialHostStartup);
        }
    }
}
