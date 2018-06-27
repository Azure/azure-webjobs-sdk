// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class FilteringTelemetryProcessor : ITelemetryProcessor
    {
        private Func<string, LogLevel, bool> _filter;
        private ITelemetryProcessor _next;

        public FilteringTelemetryProcessor(Func<string, LogLevel, bool> filter, ITelemetryProcessor next)
        {
            _filter = filter;
            _next = next;
        }

        public void Process(ITelemetry item)
        {
            if (IsEnabled(item))
            {
                _next.Process(item);
            }
        }

        private bool IsEnabled(ITelemetry item)
        {
            bool enabled = true;

            ISupportProperties telemetry = item as ISupportProperties;

            if (telemetry != null && _filter != null)
            {
                string categoryName = null;
                if (!telemetry.Properties.TryGetValue(LogConstants.CategoryNameKey, out categoryName))
                {
                    // If no category is specified, it will be filtered by the default filter
                    categoryName = string.Empty;

                    // WebJobs host does many internal calls, polling queues and blobs, etc...
                    // we do not want to report all of them by default, but only those which are relevant for  
                    // function execution: bindings and user code (which have category and level stamped on the telemetry).
                    // So, if there is no category on the operation telemtery (request or dependency),
                    // it won't be tracked unless filter explicitly enables it
                    if (telemetry is OperationTelemetry)
                    {
                        enabled = false;
                    }
                }

                // Extract the log level and apply the filter
                string logLevelString = null;
                LogLevel logLevel;
                if (telemetry.Properties.TryGetValue(LogConstants.LogLevelKey, out logLevelString) &&
                    Enum.TryParse(logLevelString, out logLevel))
                {
                    enabled = _filter(categoryName, logLevel);
                }
            }

            return enabled;
        }
    }
}
