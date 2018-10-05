// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class FilteringTelemetryProcessor : ITelemetryProcessor
    {
        private static readonly LoggerRuleSelector RuleSelector = new LoggerRuleSelector();
        private static readonly Type ProviderType = typeof(ApplicationInsightsLoggerProvider);

        private readonly ConcurrentDictionary<string, LoggerFilterRule> _ruleMap = new ConcurrentDictionary<string, LoggerFilterRule>();
        private readonly LoggerFilterOptions _filterOptions;
        private ITelemetryProcessor _next;

        public FilteringTelemetryProcessor(LoggerFilterOptions filterOptions, ITelemetryProcessor next)
        {
            _filterOptions = filterOptions;
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

            if (item is ISupportProperties telemetry && _filterOptions != null)
            {
                if (!telemetry.Properties.TryGetValue(LogConstants.CategoryNameKey, out string categoryName))
                {
                    // If no category is specified, it will be filtered by the default filter
                    categoryName = string.Empty;
                }

                // Extract the log level and apply the filter
                if (telemetry.Properties.TryGetValue(LogConstants.LogLevelKey, out string logLevelString) &&
                    Enum.TryParse(logLevelString, out LogLevel logLevel))
                {
                    LoggerFilterRule filterRule = _ruleMap.GetOrAdd(categoryName, SelectRule(categoryName));

                    if (filterRule.LogLevel != null && logLevel < filterRule.LogLevel)
                    {
                        enabled = false;
                    }
                    else if (filterRule.Filter != null)
                    {
                        enabled = filterRule.Filter(ProviderType.FullName, categoryName, logLevel);
                    }
                }
            }

            return enabled;
        }

        private LoggerFilterRule SelectRule(string categoryName)
        {
            RuleSelector.Select(_filterOptions, ProviderType, categoryName,
                out LogLevel? minLevel, out Func<string, string, LogLevel, bool> filter);

            return new LoggerFilterRule(ProviderType.FullName, categoryName, minLevel, filter);
        }
    }
}
