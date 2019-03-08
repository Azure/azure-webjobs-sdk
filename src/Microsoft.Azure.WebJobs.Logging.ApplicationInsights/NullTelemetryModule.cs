// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    /// <summary>
    /// Noop telemetry module that is added instead of another one, which is disabled by settings.
    /// </summary>
    internal class NullTelemetryModule : ITelemetryModule
    {
        public static NullTelemetryModule Instance { get; } = new NullTelemetryModule();

        private NullTelemetryModule()
        {
        }

        public void Initialize(TelemetryConfiguration configuration)
        {
        }
    }
}
