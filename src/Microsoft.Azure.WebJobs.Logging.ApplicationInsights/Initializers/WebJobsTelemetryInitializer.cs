﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class WebJobsTelemetryInitializer : ITelemetryInitializer
    {
        private const string ComputerNameKey = "COMPUTERNAME";
        private const string WebSiteInstanceIdKey = "WEBSITE_INSTANCE_ID";

        private static readonly string _instanceId = GetSiteInstanceId();

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            RequestTelemetry request = telemetry as RequestTelemetry;

            // Zero out all IP addresses other than Requests
            if (request == null)
            {
                telemetry.Context.Location.Ip = LoggingConstants.ZeroIpAddress;
            }
            else
            {
                if (request.Context.Location.Ip == null)
                {
                    request.Context.Location.Ip = LoggingConstants.ZeroIpAddress;
                }
            }

            IDictionary<string, string> telemetryProps = telemetry.Context.Properties;

            // Apply our special scope properties
            IDictionary<string, object> scopeProps =
                DictionaryLoggerScope.GetMergedStateDictionary() ?? new Dictionary<string, object>();

            string invocationId = scopeProps.GetValueOrDefault<string>(ScopeKeys.FunctionInvocationId);
            if (invocationId != null)
            {
                telemetryProps[LogConstants.InvocationIdKey] = invocationId;
            }

            string functionName = scopeProps.GetValueOrDefault<string>(ScopeKeys.FunctionName);

            // this could be telemetry tracked in scope of function call - then we should apply the logger scope
            // or RequestTelemetry tracked by the WebJobs SDK or AppInsight SDK - then we should apply Activity.Tags
            if (request == null && scopeProps.Any())
            {
                telemetry.Context.Operation.Name = functionName;
                telemetry.Context.Cloud.RoleInstance = functionName ?? _instanceId;

                // Apply Category and LogLevel to all telemetry
                string category = scopeProps.GetValueOrDefault<string>(LogConstants.CategoryNameKey);
                if (category != null)
                {
                    telemetryProps[LogConstants.CategoryNameKey] = category;
                }

                LogLevel? logLevel = scopeProps.GetValueOrDefault<LogLevel?>(LogConstants.LogLevelKey);
                if (logLevel != null)
                {
                    telemetryProps[LogConstants.LogLevelKey] = logLevel.Value.ToString();
                }

                int? eventId = scopeProps.GetValueOrDefault<int?>(LogConstants.EventIdKey);
                if (eventId != null && eventId.HasValue && eventId.Value != 0)
                {
                    telemetryProps[LogConstants.EventIdKey] = eventId.Value.ToString();
                }
            }
            // we may track traces/dependencies after function scope ends - we don't want to update those
            else if (request != null)
            {
                Activity currentActivity = Activity.Current;
                if (currentActivity != null) // should never be null, but we don't want to throw anyway
                {
                    // tags is a list, we'll enumerate it
                    foreach (var tag in currentActivity.Tags)
                    {
                        switch (tag.Key)
                        {
                            case LogConstants.NameKey:
                                request.Name = tag.Value;
                                request.Context.Operation.Name = tag.Value;
                                request.Context.Cloud.RoleInstance = tag.Value;
                                break;
                            case LogConstants.FullNameKey:
                                break;
                            default:
                                request.Properties[tag.Key] = tag.Value;
                                break;
                        }
                    }
                }
                else // workaround for https://github.com/Microsoft/ApplicationInsights-dotnet-server/issues/1038
                {
                    if (functionName != null || request.Properties.TryGetValue(LogConstants.NameKey, out functionName))
                    {
                        request.Name = functionName;
                        request.Context.Operation.Name = functionName;
                        request.Context.Cloud.RoleInstance = functionName;
                    }
                }
            }
        }

        private static string GetSiteInstanceId()
        {
            string instanceId = Environment.GetEnvironmentVariable(WebSiteInstanceIdKey);
            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = Environment.GetEnvironmentVariable(ComputerNameKey);
            }

            return instanceId;
        }
    }
}
