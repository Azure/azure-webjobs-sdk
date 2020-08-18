// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class NullTelemetryInitializer : ITelemetryInitializer
    {
        public static NullTelemetryInitializer Instance { get; } = new NullTelemetryInitializer();
        public void Initialize(ITelemetry telemetry)
        {
        }
    }
}
