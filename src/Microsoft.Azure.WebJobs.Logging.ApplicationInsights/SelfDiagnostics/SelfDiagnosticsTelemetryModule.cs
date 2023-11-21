// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    /// <summary>    
    /// Initializes <see cref="ApplicationInsightsEventListener"/> that listens to events produced by ApplicationInsights SDK.
    /// </summary>
    internal class SelfDiagnosticsTelemetryModule : ITelemetryModule, IDisposable
    {
        private ApplicationInsightsEventListener _eventListener;
        private EventLevel _eventLevel;

        internal SelfDiagnosticsTelemetryModule(EventLevel eventLevel)
        {
            _eventLevel = eventLevel;
        }

        public void Initialize(TelemetryConfiguration configuration)
        {
            _eventListener = new ApplicationInsightsEventListener(_eventLevel);
        }
        
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _eventListener?.Dispose();
            }
        }
    }
}