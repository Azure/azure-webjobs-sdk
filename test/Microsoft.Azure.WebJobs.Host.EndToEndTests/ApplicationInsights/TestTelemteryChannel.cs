// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class TestTelemetryChannel : ITelemetryChannel, ITelemetryModule
    {
        public ConcurrentBag<ITelemetry> Telemetries = new ConcurrentBag<ITelemetry>();

        public bool? DeveloperMode { get; set; }

        public string EndpointAddress { get; set; }

        public void Dispose()
        {
        }

        public void Flush()
        {
        }

        public void Send(ITelemetry item)
        {
            Telemetries.Add(item);
        }

        public void Initialize(TelemetryConfiguration configuration)
        {
        }
    }
}
