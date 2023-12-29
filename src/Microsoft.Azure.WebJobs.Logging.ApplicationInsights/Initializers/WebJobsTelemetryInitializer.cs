// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class WebJobsTelemetryInitializer : ITelemetryInitializer
    {
        private static readonly string _currentProcessId = Process.GetCurrentProcess().Id.ToString();
        private readonly string _sdkVersion;
        private readonly string _roleInstanceName;
        private readonly ApplicationInsightsLoggerOptions _options;

        public WebJobsTelemetryInitializer(ISdkVersionProvider versionProvider, IRoleInstanceProvider roleInstanceProvider, IOptions<ApplicationInsightsLoggerOptions> options)
        {
            if (versionProvider == null)
            {
                throw new ArgumentNullException(nameof(versionProvider));
            }

            if (roleInstanceProvider == null)
            {
                throw new ArgumentNullException(nameof(roleInstanceProvider));
            }

            _sdkVersion = versionProvider.GetSdkVersion();
            _roleInstanceName = roleInstanceProvider.GetRoleInstanceName();
            _options = options.Value;
        }

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            var telemetryContext = telemetry.Context;
            telemetryContext.Cloud.RoleInstance = _roleInstanceName;
            if (telemetryContext.Location.Ip == null)
            {
                telemetryContext.Location.Ip = LoggingConstants.ZeroIpAddress;
            }

            // Do not apply state properties if optimization is enabled.
            if (_options.EnableMetricsCustomDimensionOptimization && telemetry is MetricTelemetry)
            {
                // Remove the Host instance ID property, since it's not needed.
                telemetryContext.Properties.Remove(LoggingConstants.HostInstanceIdKey);
                return;
            }
            else
            {
                telemetryContext.Properties[LogConstants.ProcessIdKey] = _currentProcessId;

                // Apply our special scope properties
                IReadOnlyDictionary<string, object> scopeProps = DictionaryLoggerScope.GetMergedStateDictionaryOrNull();

                // this could be telemetry tracked in scope of function call - then we should apply the logger scope
                // or RequestTelemetry tracked by the WebJobs SDK or AppInsight SDK - then we should apply Activity.Tags
                if (scopeProps?.Count > 0)
                {
                    if (!telemetryContext.Properties.ContainsKey(LogConstants.InvocationIdKey))
                    {
                        if (scopeProps?.GetValueOrDefault<string>(ScopeKeys.FunctionInvocationId) is string invocationId)
                        {
                            telemetryContext.Properties[LogConstants.InvocationIdKey] = invocationId;
                        }
                    }
                    
                    telemetryContext.Operation.Name = scopeProps.GetValueOrDefault<string>(ScopeKeys.FunctionName);

                    // Apply Category, LogLevel event details to all telemetry
                    if (!telemetryContext.Properties.ContainsKey(LogConstants.CategoryNameKey))
                    {
                        if (scopeProps.GetValueOrDefault<string>(LogConstants.CategoryNameKey) is string category)
                        {
                            telemetryContext.Properties[LogConstants.CategoryNameKey] = category;
                        }
                    }

                    if (!telemetryContext.Properties.ContainsKey(LogConstants.LogLevelKey))
                    {
                        if (scopeProps.GetValueOrDefault<LogLevel?>(LogConstants.LogLevelKey) is LogLevel logLevel)
                        {
                            telemetryContext.Properties[LogConstants.LogLevelKey] = logLevel.ToStringOptimized();
                        }
                    }

                    if (!telemetryContext.Properties.ContainsKey(LogConstants.EventIdKey))
                    {
                        if (scopeProps.GetValueOrDefault<int>(LogConstants.EventIdKey) is int eventId && eventId != 0)
                        {
                            telemetryContext.Properties[LogConstants.EventIdKey] = eventId.ToString(CultureInfo.InvariantCulture);
                        }
                    }

                    if (!telemetryContext.Properties.ContainsKey(LogConstants.EventNameKey))
                    {
                        if (scopeProps.GetValueOrDefault<string>(LogConstants.EventNameKey) is string eventName)
                        {
                            telemetryContext.Properties[LogConstants.EventNameKey] = eventName;
                        }
                    }
                }

                // we may track traces/dependencies after function scope ends - we don't want to update those
                if (telemetry is RequestTelemetry request)
                {
                    UpdateRequestProperties(request);

                    Activity currentActivity = Activity.Current;
                    if (currentActivity != null)
                    {
                        foreach (var tag in currentActivity.Tags)
                        {
                            // Apply well-known tags and custom properties, 
                            // but ignore internal ai tags
                            if (!TryApplyProperty(request, tag) &&
                                !tag.Key.StartsWith("ai_"))
                            {
                                request.Properties[tag.Key] = tag.Value;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Changes properties of the RequestTelemetry to match what Functions expects.
        /// </summary>
        /// <param name="request">The RequestTelemetry to update.</param>
        private void UpdateRequestProperties(RequestTelemetry request)
        {
            request.Context.GetInternalContext().SdkVersion = _sdkVersion;

            // If the code hasn't been set, it's not an HttpRequest (could be auto-tracked SB, etc).
            // So we'll initialize it to 0
            if (string.IsNullOrEmpty(request.ResponseCode))
            {
                request.ResponseCode = "0";
            }

            // If the Url is not null, it's an actual HttpRequest, as opposed to a
            // Service Bus or other function invocation that we're tracking as a Request
            if (request.Url != null)
            {
                if (!request.Properties.ContainsKey(LogConstants.HttpMethodKey))
                {
                    // App Insights sets request.Name as 'VERB /path'. We want to extract the VERB. 
                    var verbEnd = request.Name.IndexOf(' ');
                    if (verbEnd > 0)
                    {
                        request.Properties.Add(LogConstants.HttpMethodKey, request.Name.Substring(0, verbEnd));
                    }
                }

                if (!request.Properties.ContainsKey(LogConstants.HttpPathKey))
                {
                    request.Properties.Add(LogConstants.HttpPathKey, request.Url.LocalPath);
                }

                // sanitize request Url - remove query string
                if (!_options.EnableQueryStringTracing)
                {
                    request.Url = new Uri(request.Url.GetLeftPart(UriPartial.Path));
                }
            }
        }

        /// <summary>
        /// Tries to apply well-known properties from a KeyValuePair onto the RequestTelemetry.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="activityTag">Tag on the request activity.</param>
        /// <returns>True if the tag was applied. Otherwise, false.</returns>
        private bool TryApplyProperty(RequestTelemetry request, KeyValuePair<string, string> activityTag)
        {
            bool wasPropertySet = false;

            if (activityTag.Key == LogConstants.NameKey)
            {
                request.Context.Operation.Name = activityTag.Value;
                request.Name = activityTag.Value;

                wasPropertySet = true;
            }
            else if (activityTag.Key == LogConstants.SucceededKey &&
                bool.TryParse(activityTag.Value, out bool success))
            {
                // no matter what App Insights says about the response, we always
                // want to use the function's result for Succeeded
                request.Success = success;
                wasPropertySet = true;

                // Remove the Succeeded property if set
                if (request.Properties.ContainsKey(LogConstants.SucceededKey))
                {
                    request.Properties.Remove(LogConstants.SucceededKey);
                }
            }
            else if (activityTag.Key == LoggingConstants.ClientIpKey)
            {
                request.Context.Location.Ip = activityTag.Value;
                wasPropertySet = true;
            }

            return wasPropertySet;
        }
    }
}