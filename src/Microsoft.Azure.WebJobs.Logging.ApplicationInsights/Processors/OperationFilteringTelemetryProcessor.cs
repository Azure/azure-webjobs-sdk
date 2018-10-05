// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class OperationFilteringTelemetryProcessor : ITelemetryProcessor
    {
        private readonly ITelemetryProcessor _next;

        public OperationFilteringTelemetryProcessor(ITelemetryProcessor next)
        {
            _next = next;
        }

        public void Process(ITelemetry item)
        {
            // WebJobs host does many internal calls, polling queues and blobs, etc...
            // we do not want to report all of them by default, but only those which are relevant for
            // function execution: bindings and user code (which have category and level stamped on the telemetry).
            // So, if there is no category on the operation telemtery (request or dependency), we return.
            // This filter runs before QuickPulse to reduce logging internal 40x operations performed by the host.
            if (item is OperationTelemetry telemetry && !telemetry.Properties.ContainsKey(LogConstants.CategoryNameKey))
            {
                return;
            }

            _next.Process(item);
        }
    }
}
