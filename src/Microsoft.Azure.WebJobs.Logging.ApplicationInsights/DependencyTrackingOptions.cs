// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    public class DependencyTrackingOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to disable runtime instrumentation.
        /// </summary>
        public bool DisableRuntimeInstrumentation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to disable Http Desktop DiagnosticSource instrumentation.
        /// </summary>
        public bool DisableDiagnosticSourceInstrumentation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable legacy (x-ms*) correlation headers injection.
        /// </summary>
        public bool EnableLegacyCorrelationHeadersInjection { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable Request-Id correlation headers injection.
        /// </summary>
        public bool EnableRequestIdHeaderInjectionInW3CMode { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to track the SQL command text in SQL
        /// dependencies.
        /// </summary>
        public bool EnableSqlCommandTextInstrumentation { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the correlation headers would be set
        /// on outgoing http requests.
        /// </summary>
        public bool SetComponentCorrelationHttpHeaders { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether telemetry would be produced for Azure
        /// SDK methods calls and requests.
        /// </summary>
        public bool EnableAzureSdkTelemetryListener { get; set; } = true;
    }
}
