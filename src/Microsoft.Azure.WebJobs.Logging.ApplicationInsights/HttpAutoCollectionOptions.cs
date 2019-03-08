// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    public class HttpAutoCollectionOptions
    {
        /// <summary>
        /// Gets or sets flag that enables extended HTTP request information for HTTP triggers:
        /// incoming request correlation headers, multi instrumentation keys support, HTTP method, path and response code
        /// </summary>
        public bool EnableHttpTriggerExtendedInfoCollection { get; set; } = true;

        /// <summary>
        /// Gets or sets flag that enables support of W3C distributed tracing protocol
        /// (and turns on legacy correlation schema).  Enabled by default when <see cref="EnableHttpTriggerExtendedInfoCollection"/> is true.
        /// If <see cref="EnableHttpTriggerExtendedInfoCollection"/> is false, applies to outgoing requests, but not incoming requests.
        /// </summary>
        public bool EnableW3CDistributedTracing { get; set; } = true;

        /// <summary>
        /// Gets or sets a flag that enables injection of multi-component correlation headers into responses.
        /// This allows Application Insights to construct an Application Map to  when several
        /// instrumentation keys are used. Enabled by default when <see cref="EnableHttpTriggerExtendedInfoCollection"/> is true.
        /// Does not apply if <see cref="EnableHttpTriggerExtendedInfoCollection"/> is false.
        /// </summary>
        public bool EnableResponseHeaderInjection { get; set; } = true;
    }
}