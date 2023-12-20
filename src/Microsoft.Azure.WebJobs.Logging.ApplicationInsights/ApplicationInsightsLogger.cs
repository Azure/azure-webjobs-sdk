// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class ApplicationInsightsLogger : ILogger
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly ApplicationInsightsLoggerOptions _loggerOptions;
        private readonly string _categoryName;
        private readonly bool _isUserFunction = false;
        private static readonly ConcurrentDictionary<string, string> _prefixedProperyNames = new ConcurrentDictionary<string, string>();

        private const string DefaultCategoryName = "Default";
        private const string DateTimeFormatString = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK";
        private const string OperationContext = "MS_OperationContext";

        internal const string MetricCountKey = "count";
        internal const string MetricMinKey = "min";
        internal const string MetricMaxKey = "max";
        internal const string MetricStandardDeviationKey = "standarddeviation";

        private static readonly HashSet<string> SystemScopeKeys = new HashSet<string>
            {
                LogConstants.CategoryNameKey,
                LogConstants.LogLevelKey,
                LogConstants.EventIdKey,
                LogConstants.EventNameKey,
                LogConstants.OriginalFormatKey,
                ApplicationInsightsScopeKeys.HttpRequest,
                ScopeKeys.Event,
                ScopeKeys.FunctionInvocationId,
                ScopeKeys.FunctionName,
                ScopeKeys.HostInstanceId,
                OperationContext,
                ScopeKeys.TriggerDetails
            };

        public ApplicationInsightsLogger(TelemetryClient client, string categoryName, ApplicationInsightsLoggerOptions loggerOptions)
        {
            _telemetryClient = client;
            _loggerOptions = loggerOptions;
            _categoryName = categoryName ?? DefaultCategoryName;
            _isUserFunction = LogCategories.IsFunctionUserCategory(categoryName);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception exception, Func<TState, Exception, string> formatter)
        {
            IEnumerable<KeyValuePair<string, object>> stateValues = state as IEnumerable<KeyValuePair<string, object>>;

            // If we don't have anything here, there's nothing to log.
            if (stateValues == null && formatter == null && exception == null)
            {
                return;
            }

            // Initialize stateValues so the rest of the methods don't have to worry about null values.
            stateValues = stateValues ?? Array.Empty<KeyValuePair<string, object>>();

            if (_isUserFunction && eventId.Id == LogConstants.MetricEventId)
            {
                // Log a metric from user logs only
                LogMetric(stateValues, logLevel, eventId);
            }
            else if (_categoryName == LogCategories.Results)
            {
                // Log a function result
                LogFunctionResult(stateValues, logLevel, exception, eventId);
            }
            else if (_categoryName == LogCategories.Aggregator)
            {
                // Log an aggregate record
                LogFunctionResultAggregate(stateValues, logLevel, eventId);
            }
            else
            {
                string formattedMessage = formatter?.Invoke(state, exception);

                if (exception != null)
                {
                    // Log an exception
                    LogException(logLevel, stateValues, exception, formattedMessage, eventId);
                }
                else
                {
                    // Otherwise, log a trace
                    LogTrace(logLevel, stateValues, formattedMessage, eventId);
                }
            }
        }

        private void LogMetric(IEnumerable<KeyValuePair<string, object>> values, LogLevel logLevel, EventId eventId)
        {
            MetricTelemetry telemetry = new MetricTelemetry();
            // Always apply scope first to allow state to override.
            ApplyScopeProperties(telemetry);

            // Do not apply state properties if optimization is enabled
            if (!_loggerOptions.EnableMetricsCustomDimensionOptimization)
            {
                // Add known properties like category, logLevel and event
                ApplyKnownProperties(telemetry.Properties, logLevel, eventId);
            }

            foreach (var entry in values)
            {
                if (entry.Value == null)
                {
                    continue;
                }

                // Name and Value are not lower-case so check for them explicitly first and move to the
                // next entry if found.
                switch (entry.Key)
                {
                    case LogConstants.NameKey when entry.Value is string name:
                        telemetry.Name = name;
                        continue;
                    case LogConstants.MetricValueKey when entry.Value is double sum:
                        telemetry.Sum = sum;
                        continue;
                    case LogConstants.OriginalFormatKey:
                        continue;
                    default:
                        break;
                }

                // Now check for case-insensitive matches
                switch (entry.Key.ToLowerInvariant())
                {
                    case MetricCountKey when entry.Value is int count:
                        telemetry.Count = count;
                        break;
                    case MetricMinKey:
                        telemetry.Min = Convert.ToDouble(entry.Value);
                        break;
                    case MetricMaxKey:
                        telemetry.Max = Convert.ToDouble(entry.Value);
                        break;
                    case MetricStandardDeviationKey:
                        telemetry.StandardDeviation = Convert.ToDouble(entry.Value);
                        break;
                    default:
                        // Otherwise, it's a custom property.
                        ApplyProperty(telemetry, entry, true);
                        break;
                }
            }

            _telemetryClient.TrackMetric(telemetry);
        }

        // Applies scope properties; filters most system properties, which are used internally
        private static void ApplyScopeProperties(ITelemetry telemetry)
        {
            var scopeProperties = DictionaryLoggerScope.GetMergedStateDictionaryOrNull();
            if (scopeProperties != null)
            {
                foreach (var scopeProperty in scopeProperties)
                {
                    if (scopeProperty.Value != null && !SystemScopeKeys.Contains(scopeProperty.Key, StringComparer.Ordinal))
                    {
                        ApplyProperty(telemetry.Context.Properties, scopeProperty.Key, scopeProperty.Value, true);
                    }
                }

                if (scopeProperties.GetValueOrDefault<string>(ScopeKeys.FunctionInvocationId) is string invocationId)
                {
                    telemetry.Context.Properties[LogConstants.InvocationIdKey] = invocationId;
                }
            }
            telemetry.Context.Operation.Name = scopeProperties.GetValueOrDefault<string>(ScopeKeys.FunctionName);
        }

        private void LogException(LogLevel logLevel, IEnumerable<KeyValuePair<string, object>> values, Exception exception, string formattedMessage, EventId eventId)
        {
            ExceptionTelemetry telemetry = new ExceptionTelemetry(exception)
            {
                SeverityLevel = GetSeverityLevel(logLevel),
                Timestamp = DateTimeOffset.UtcNow
            };

            if (!string.IsNullOrEmpty(formattedMessage))
            {
                telemetry.Properties[LogConstants.FormattedMessageKey] = formattedMessage;

                // Also log a trace if there's a formattedMessage. This ensures that the error is visible
                // in both App Insights analytics tables.
                LogTrace(logLevel, values, formattedMessage, eventId);
            }

            ApplyScopeAndStateProperties(telemetry.Properties, values, telemetry);
            ApplyKnownProperties(telemetry.Properties, logLevel, eventId);

            _telemetryClient.TrackException(telemetry);
        }

        private void LogTrace(LogLevel logLevel, IEnumerable<KeyValuePair<string, object>> values, string formattedMessage, EventId eventId)
        {
            TraceTelemetry telemetry = new TraceTelemetry(formattedMessage);

            ApplyScopeAndStateProperties(telemetry.Properties, values, telemetry);

            ApplyKnownProperties(telemetry.Properties, logLevel, eventId);

            var severityLevel = GetSeverityLevel(logLevel);


            if (severityLevel.HasValue)
            {
                telemetry.SeverityLevel = severityLevel;
            }

            // LogLevel.None maps to null, so we have to handle that specially
            _telemetryClient.TrackTrace(telemetry);
        }

        private static SeverityLevel? GetSeverityLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    return SeverityLevel.Verbose;
                case LogLevel.Information:
                    return SeverityLevel.Information;
                case LogLevel.Warning:
                    return SeverityLevel.Warning;
                case LogLevel.Error:
                    return SeverityLevel.Error;
                case LogLevel.Critical:
                    return SeverityLevel.Critical;
                case LogLevel.None:
                default:
                    return null;
            }
        }

        // Makes sure these are done in the correct order. If there are duplicate keys, the last State property wins.
        private static void ApplyScopeAndStateProperties(IDictionary<string, string> properties, IEnumerable<KeyValuePair<string, object>> state, ITelemetry telemetry)
        {
            ApplyScopeProperties(telemetry);
            ApplyProperties(properties, state, true);
        }

        internal void ApplyKnownProperties(IDictionary<string, string> properties, LogLevel logLevel, EventId eventId)
        {
            properties[LogConstants.CategoryNameKey] = _categoryName;
            properties[LogConstants.LogLevelKey] = logLevel.ToStringOptimized();

            if (eventId.Id != 0)
            {
                properties[LogConstants.EventIdKey] = Convert.ToString(eventId.Id);
            }
            if (!string.IsNullOrEmpty(eventId.Name))
            {
                properties[LogConstants.EventNameKey] = eventId.Name;
            }
        }
        internal static void ApplyProperty(IDictionary<string, string> properties, string key, object objectValue, bool applyPrefix = false)
        {
            // do not apply null values
            if (objectValue == null)
            {
                return;
            }

            string stringValue = null;

            // Format dates
            if (objectValue is string value)
            {
                stringValue = value;
            }
            else if (objectValue is DateTime date)
            {
                stringValue = date.ToUniversalTime().ToString(DateTimeFormatString);
            }
            else if (objectValue is DateTimeOffset dateOffset)
            {
                stringValue = dateOffset.UtcDateTime.ToString(DateTimeFormatString);
            }
            else
            {
                stringValue = objectValue.ToString();
            }

            string prefixedKey = applyPrefix ? _prefixedProperyNames.GetOrAdd(key, k =>
            {
                return $"{LogConstants.CustomPropertyPrefix}{k}";
            }) : key;
            properties[prefixedKey] = stringValue;
        }

        private static void ApplyProperty(ISupportProperties telemetry, KeyValuePair<string, object> value, bool applyPrefix = false)
        {
            ApplyProperty(telemetry.Properties, value.Key, value.Value, applyPrefix);
        }

        // Inserts properties into the telemetry's properties. Properly formats dates, removes nulls, applies prefix, etc.
        private static void ApplyProperties(IDictionary<string, string> properties, IEnumerable<KeyValuePair<string, object>> values, bool applyPrefix = false)
        {
            foreach (var property in values)
            {
                ApplyProperty(properties, property.Key, property.Value, applyPrefix);
            }
        }

        private void LogFunctionResultAggregate(IEnumerable<KeyValuePair<string, object>> values, LogLevel logLevel, EventId eventId)
        {
            // Metric names will be created like "{FunctionName} {MetricName}"
            IDictionary<string, double> metrics = new Dictionary<string, double>(7);
            string functionName = LoggingConstants.Unknown;

            // build up the collection of metrics to send
            foreach (KeyValuePair<string, object> value in values)
            {
                switch (value.Key)
                {
                    case LogConstants.NameKey when value.Value is string name:
                        functionName = name;
                        break;
                    case LogConstants.TimestampKey:
                    case LogConstants.OriginalFormatKey:
                        // Timestamp is created automatically
                        // We won't use the format string here
                        break;
                    default:
                        if (value.Value is int intValue)
                        {
                            metrics.Add(value.Key, Convert.ToDouble(intValue));
                        }
                        else if (value.Value is double doubleValue)
                        {
                            metrics.Add(value.Key, doubleValue);
                        }
                        else if (value.Value is TimeSpan timeSpan)
                        {
                            // if it's a TimeSpan, log the milliseconds
                            metrics.Add(value.Key, timeSpan.TotalMilliseconds);
                        }
                        // do nothing otherwise
                        break;
                }
            }

            IDictionary<string, string> properties = null;

            // Do not apply state properties if optimization is enabled
            if (!_loggerOptions.EnableMetricsCustomDimensionOptimization)
            {
                properties = new Dictionary<string, string>(2);
                ApplyKnownProperties(properties, logLevel, eventId);
            }

            foreach (KeyValuePair<string, double> metric in metrics)
            {
                _telemetryClient.TrackMetric($"{functionName} {metric.Key}", metric.Value, properties);
            }
        }

        private void LogFunctionResult(IEnumerable<KeyValuePair<string, object>> state, LogLevel logLevel, Exception exception, EventId eventId)
        {
            IReadOnlyDictionary<string, object> scopeProps = DictionaryLoggerScope.GetMergedStateDictionaryOrNull();
            KeyValuePair<string, object>[] stateProps = state as KeyValuePair<string, object>[] ?? state.ToArray();

            // log associated exception details
            if (exception != null)
            {
                LogException(logLevel, stateProps, exception, null, eventId);
            }

            ApplyFunctionResultActivityTags(stateProps, scopeProps, logLevel);

            IOperationHolder<RequestTelemetry> requestOperation = scopeProps?.GetValueOrDefault<IOperationHolder<RequestTelemetry>>(OperationContext);
            if (requestOperation != null)
            {
                // We somehow never started the operation, perhaps, it was auto-tracked by the AI SDK
                // so there's no way to complete it.

                RequestTelemetry requestTelemetry = requestOperation.Telemetry;
                requestTelemetry.Success = exception == null;
                requestTelemetry.ResponseCode = "0";

                // Note: we do not have to set Duration, StartTime, etc. These are handled by the call to Stop()
                _telemetryClient.StopOperation(requestOperation);
            }
        }

        /// <summary>
        /// Stamps functions attributes (InvocationId, function execution time, Category and LogLevel) on the Activity.Current
        /// </summary>
        /// <param name="state"></param>
        /// <param name="scope"></param>
        private void ApplyFunctionResultActivityTags(IEnumerable<KeyValuePair<string, object>> state, IReadOnlyDictionary<string, object> scope, LogLevel logLevel)
        {
            // Activity carries tracing context. It is managed by instrumented library (e.g. ServiceBus or Asp.Net Core)
            // and consumed by ApplicationInsights.
            // This function stamps all function-related tags on the Activity. Then WebJobsTelemetryInitializer sets them on the RequestTelemetry.
            // This way, requests reported by WebJobs (e.g. timer trigger) and requests reported by ApplicationInsights (Http, ServiceBus)
            // both have essential information about function execution
            Activity currentActivity = Activity.Current;

            // should always be true
            if (currentActivity != null)
            {
                // Build up the telemetry model. Some values special and go right on the telemetry object. All others
                // are added to the Properties bag.
                foreach (KeyValuePair<string, object> prop in state)
                {
                    switch (prop.Key)
                    {
                        case LogConstants.StartTimeKey:
                        case LogConstants.EndTimeKey:
                            // These values are set by the calls to Start/Stop the telemetry. Other
                            // Loggers may want them, but we'll ignore.
                            break;
                        case LogConstants.LogLevelKey:
                        case LogConstants.CategoryNameKey:
                        case LogConstants.EventIdKey:
                            // this is set in the WebJobs initializer,
                            // we will ignore it here
                            break;
                        case LogConstants.MessageEnqueuedTimeKey:
                            // this is populated when creating telemetry
                            // we will ignore it here
                            break;
                        case LogConstants.DurationKey:
                            if (prop.Value is TimeSpan duration)
                            {
                                currentActivity.AddTag(LogConstants.FunctionExecutionTimeKey, duration.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
                            }
                            break;
                        default:
                            // There should be no custom properties here, so just copy
                            // the passed-in values without any 'prop__' prefix.
                            if (prop.Value != null)
                            {
                                currentActivity.AddTag(prop.Key, prop.Value.ToString());
                            }

                            break;
                    }
                }

                currentActivity.AddTag(LogConstants.CategoryNameKey, _categoryName);
                currentActivity.AddTag(LogConstants.LogLevelKey, logLevel.ToStringOptimized());

                if (scope != null)
                {
                    if (!_loggerOptions.HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection &&
                        scope.TryGetValue(ApplicationInsightsScopeKeys.HttpRequest, out var request) &&
                        request is HttpRequest httpRequest)
                    {
                        currentActivity.AddTag(LoggingConstants.ClientIpKey, GetIpAddress(httpRequest));
                    }
                }
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            // Filtering will occur in the Application Insights pipeline. This allows for the QuickPulse telemetry
            // to always be sent, even if logging actual records is completely disabled.
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            StartTelemetryIfFunctionInvocation(state as IDictionary<string, object>);
            return DictionaryLoggerScope.Push(state);
        }

        private void StartTelemetryIfFunctionInvocation(IDictionary<string, object> stateValues)
        {
            if (stateValues == null)
            {
                return;
            }

            var allScopes = DictionaryLoggerScope.GetMergedStateDictionaryOrNull();
            // HTTP and ServiceBus triggers are tracked automatically by the ApplicationInsights SDK
            // In such case a current Activity is present.
            // We won't track and only stamp function specific details on the RequestTelemetry
            // created by SDK via Activity when function ends

            var currentActivity = Activity.Current;
            if (currentActivity == null ||
                // Activity is tracked, but Functions wants to ignore it:
                (allScopes != null && allScopes.ContainsKey("MS_IgnoreActivity")) ||
                // Functions create another RequestTrackingTelemetryModule to make sure first request is tracked (as ASP.NET Core starts before web jobs)
                // however at this point we may discover that RequestTrackingTelemetryModule is disabled by customer and even though Activity exists, request won't be tracked
                // So, if we've got AspNetCore Activity and EnableHttpTriggerExtendedInfoCollection is false - track request here.
                (!_loggerOptions.HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection && IsHttpRequestActivity(currentActivity)))
            {
                string functionName = stateValues.GetValueOrDefault<string>(ScopeKeys.FunctionName);
                string functionInvocationId = stateValues.GetValueOrDefault<string>(ScopeKeys.FunctionInvocationId);
                string eventName = stateValues.GetValueOrDefault<string>(ScopeKeys.Event);

                // If we have the invocation id, function name, and event, we know it's a new function. That means
                // that we want to start a new operation and let App Insights track it for us.
                if (!string.IsNullOrEmpty(functionName) &&
                    !string.IsNullOrEmpty(functionInvocationId) &&
                    eventName == LogConstants.FunctionStartEvent)
                {
                    IOperationHolder<RequestTelemetry> operation;

                    // link represents context from the upstream service that is not necessarily immediate parent
                    // it is used by EventHubs to represent context in the message.
                    // if there is just one link, we'll use it as a parent as an optimization.
                    // if there is more than one, we'll populate them as custom properties
                    IEnumerable<Activity> links = allScopes?.GetValueOrDefault<IEnumerable<Activity>>("Links");
                    var activities = links as Activity[] ?? links?.ToArray();

                    if (activities != null)
                    {
                        if (activities.Length == 1)
                        {
                            operation = _telemetryClient.StartOperation<RequestTelemetry>(activities[0]);
                            operation.Telemetry.Name = functionName;
                        }
                        else
                        {
                            operation = CreateRequestFromLinks(activities, functionName);
                        }

                        if (this.TryGetAverageTimeInQueueForBatch(activities, operation.Telemetry.Timestamp, out long enqueuedTime))
                        {
                            operation.Telemetry.Metrics["timeSinceEnqueued"] = enqueuedTime;
                        }
                    }
                    else
                    {
                        operation = _telemetryClient.StartOperation<RequestTelemetry>(functionName);
                    }

                    var triggerDetails = stateValues.GetValueOrDefault<IDictionary<string, string>>(ScopeKeys.TriggerDetails);
                    if (triggerDetails != null)
                    {
                        triggerDetails.TryGetValue(LogConstants.TriggerDetailsEndpointKey, out var endpoint);
                        triggerDetails.TryGetValue(LogConstants.TriggerDetailsEntityNameKey, out var entity);

                        if (endpoint != null && entity != null)
                        {
                            operation.Telemetry.Source = endpoint.EndsWith("/") ? string.Concat(endpoint, entity) : string.Concat(endpoint, "/", entity);
                        }
                        else if (endpoint != null)
                        {
                            operation.Telemetry.Source = endpoint;
                        }
                        else if (entity != null)
                        {
                            operation.Telemetry.Source = entity;
                        }
                    }

                    // We'll need to store this operation context so we can stop it when the function completes
                    stateValues[OperationContext] = operation;
                }
            }
            // If there is a current activity, it is assumed that Application Insights will track it so we do not start an operation.
            // However, in some cases (such as Durable functions), this is not the case. This allows the scope to decide whether
            // an operation should be started, even when the current activity is not null.
            else if (allScopes != null && allScopes.ContainsKey("MS_TrackActivity"))
            {
                var operation = _telemetryClient.StartOperation<RequestTelemetry>(currentActivity);
                stateValues[OperationContext] = operation;
            }
        }

        private IOperationHolder<RequestTelemetry> CreateRequestFromLinks(Activity[] activities, string functionName)
        {
            var operation = _telemetryClient.StartOperation<RequestTelemetry>(functionName);
            var request = operation.Telemetry;
            // if there is more than one link (batch dispatch in EventHub trigger)
            // we'll populate link information on the request telemetry, but don't touch parent for this request
            PopulateLinks(activities, request);

            // If any of the links is sampled in (on upstream) we also preliminary sample in
            // request telemetry and everything that happens in this request scope.
            // There will be additional level of sampling applied to limit rate to one
            // configured on this Function/WebJob
            if (request.ProactiveSamplingDecision == SamplingDecision.SampledIn)
            {
                Activity.Current.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }

            return operation;
        }

        private void PopulateLinks(Activity[] activities, RequestTelemetry request)
        {
            if (activities.Any(l => l.Recorded))
            {
                request.ProactiveSamplingDecision = SamplingDecision.SampledIn;
            }

            var linksJson = new StringBuilder();
            linksJson.Append('[');
            foreach (var link in activities)
            {
                var linkTraceId = link.TraceId.ToHexString();

                // avoiding json serializers for now for the sake of perf.
                // serialization is trivial and looks like `_MS.links` property with json blob
                // [{"operation_Id":"5eca8b153632494ba00f619d6877b134","id":"d4c1279b6e7b7c47"},
                //  {"operation_Id":"ff28988d0776b44f9ca93352da126047","id":"bf4fa4855d161141"}]
                linksJson
                    .Append('{')
                    .Append("\"operation_Id\":")
                    .Append('\"')
                    .Append(linkTraceId)
                    .Append('\"')
                    .Append(',');
                linksJson
                    .Append("\"id\":")
                    .Append('\"')
                    .Append(link.ParentSpanId.ToHexString())
                    .Append('\"');

                // we explicitly ignore sampling flag, tracestate and attributes at this point.
                linksJson.Append("},");
            }

            if (linksJson.Length > 0)
            {
                // remove last comma - trailing commas are not allowed
                linksJson.Remove(linksJson.Length - 1, 1);
            }

            linksJson.Append("]");

            request.Properties["_MS.links"] = linksJson.ToString();
        }

        private bool IsHttpRequestActivity(Activity activity)
        {
            // Http Activity could be created by ASP.NET Core and then is has OperationName = "Microsoft.AspNetCore.Hosting.HttpRequestIn"
            // or it could be created by ApplicationInsights in certain scenarios (like W3C support until it is integrated into the ASP.NET Core)
            // ApplicationInsights Activity is called "ActivityCreatedByHostingDiagnosticListener"
            // Here we check if activity passed is one of those.
            return activity != null &&
                   (activity.OperationName == "Microsoft.AspNetCore.Hosting.HttpRequestIn" ||
                    activity.OperationName == "ActivityCreatedByHostingDiagnosticListener");
        }

        internal static string GetIpAddress(HttpRequest httpRequest)
        {
            // first check for X-Forwarded-For; used by load balancers
            if (httpRequest.Headers?.TryGetValue(ApplicationInsightsScopeKeys.ForwardedForHeaderName, out StringValues headerValues) ?? false)
            {
                string ip = headerValues.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(ip))
                {
                    return RemovePort(ip);
                }
            }

            return httpRequest.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? LoggingConstants.ZeroIpAddress;
        }

        private static string RemovePort(string address)
        {
            // For Web sites in Azure header contains ip address with port e.g. 50.47.87.223:54464
            int portSeparatorIndex = address.IndexOf(":", StringComparison.OrdinalIgnoreCase);

            if (portSeparatorIndex > 0)
            {
                return address.Substring(0, portSeparatorIndex);
            }

            return address;
        }

        private bool TryGetAverageTimeInQueueForBatch(Activity[] links, DateTimeOffset requestStartTime, out long avgTimeInQueue)
        {
            avgTimeInQueue = 0;
            int linksCount = 0;
            foreach (var link in links)
            {
                if (!this.TryGetEnqueuedTime(link, out var msgEnqueuedTime))
                {
                    // instrumentation does not consistently report enqueued time, ignoring whole span
                    return false;
                }

                avgTimeInQueue += Math.Max(requestStartTime.ToUnixTimeMilliseconds() - msgEnqueuedTime, 0);
                linksCount++;
            }

            if (linksCount == 0)
            {
                return false;
            }

            avgTimeInQueue /= linksCount;
            return true;
        }

        private bool TryGetEnqueuedTime(Activity link, out long enqueuedTime)
        {
            enqueuedTime = 0;
            foreach (var tag in link.Tags)
            {
                if (tag.Key == LogConstants.MessageEnqueuedTimeKey)
                {
                    return long.TryParse(tag.Value, out enqueuedTime);
                }
            }

            return false;
        }
    }
}
