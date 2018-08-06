// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// This rule is used to replace any rules registered for the App Insights provider and store
    /// them all in ChildRules. When constructing the <see cref="FilteringTelemetryProcessor"/>, these
    /// rules are pulled out and applied. This allows the .NET logging infrastructure and configuration
    /// to work as usual, but still allow the logs to flow through the QuickPulse processor.
    /// </summary>
    internal class ApplicationInsightsLoggerFilterRule : LoggerFilterRule
    {
        public ApplicationInsightsLoggerFilterRule(IList<LoggerFilterRule> childRules)
            : base(typeof(ApplicationInsightsLoggerProvider).FullName, null, Logging.LogLevel.Trace, null)
        {
            ChildRules = childRules;
        }

        public IList<LoggerFilterRule> ChildRules { get; }
    }
}
