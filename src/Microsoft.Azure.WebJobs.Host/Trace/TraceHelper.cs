// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Trace
{
    internal class TraceHelper
    {
        // Not using version as the instrumented and instrumentation libraries are same
        static readonly ActivitySource ActivitySource = new ActivitySource(TraceConstants.FunctionsActivitySource, TraceConstants.FunctionsActivitySourceVersion);
        public static Activity StartInvocationActivity(string invocationId, string functionName, string funcFullName, string funcinstanceid)
        {
            Activity activity = ActivitySource.StartActivity(functionName, ActivityKind.Internal);
            if (activity != null && activity.IsAllDataRequested)
            {
                //activity.SetTag(TraceConstants.AttributeFaasTrigger, functionName);
                activity.SetTag(TraceConstants.AttributeFaasExecution, invocationId);
                activity.SetTag(TraceConstants.AttributeCloudProviderKey, TraceConstants.AttributeCloudProviderValue);
                activity.SetTag(TraceConstants.AttributeCloudPlatformKey, TraceConstants.AttributeCloudPlatformValue);
                activity.SetTag(TraceConstants.AttributeFaasNameKey, funcFullName);
                activity.SetTag(TraceConstants.AttributeFaasInstanceKey, funcinstanceid);
            }
            return activity;
        }
    }
}