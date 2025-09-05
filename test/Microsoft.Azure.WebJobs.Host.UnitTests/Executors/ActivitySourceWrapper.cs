using Microsoft.Azure.WebJobs.Host.Abstractions;
using Microsoft.Azure.WebJobs.Host.Executors;
using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Executors
{
    public class ActivitySourceWrapper : IActivitySourceAbstraction
    {
        private readonly ActivitySource _activitySource;

        public ActivitySourceWrapper(string sourceName)
        {
            _activitySource = new ActivitySource(sourceName);
        }

        public IDisposable? StartActivity(IFunctionInstanceEx functionInstance)
        {
            if (Activity.Current != null)
            {
                return null;
            }

            return _activitySource.StartActivity(functionInstance.FunctionDescriptor.LogName, ActivityKind.Server);
        }
    }   
}