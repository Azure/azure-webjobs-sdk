using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace SampleHost
{
    internal class CustomFunctionActivityStatusProvider : IFunctionActivityStatusProvider
    {
        private readonly IFunctionActivityStatusProvider _functionActivityStatusProvider;

        public CustomFunctionActivityStatusProvider(IFunctionActivityStatusProvider functionActivityStatusProvider) 
        { 
            _functionActivityStatusProvider = functionActivityStatusProvider;
        }

        public FunctionActivityStatus GetStatus()
        {
            var status = _functionActivityStatusProvider.GetStatus();

            // TODO : Add custom logic here to modify the status
            // as needed to account for in progress workflows

            return status;
        }
    }
}
