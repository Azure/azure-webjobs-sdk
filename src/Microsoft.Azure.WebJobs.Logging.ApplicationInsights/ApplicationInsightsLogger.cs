// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.W3C;
using Microsoft.AspNetCore.Http;
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

        private const string DefaultCategoryName = "Default";
        private const string DateTimeFormatString = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK";
        private const string OperationContext = "MS_OperationContext";

        internal const string MetricCountKey = "count";
        internal const string MetricMinKey = "min";
        internal const string MetricMaxKey = "max";
        internal const string MetricStandardDeviationKey = "standarddeviation";

        private static readonly string[] SystemScopeKeys =
            {
                LogConstants.CategoryNameKey,
                LogConstants.LogLevelKey,
                LogConstants.EventIdKey,
                LogConstants.OriginalFormatKey,
                ScopeKeys.Event,
                ScopeKeys.FunctionInvocationId,
                ScopeKeys.FunctionName,
                ScopeKeys.HostInstanceId,
                OperationContext
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
            string formattedMessage = formatter?.Invoke(state, exception);
            IEnumerable<KeyValuePair<string, object>> stateValues = state as IEnumerable<KeyValuePair<string, object>>;

            // If we don't have anything here, there's nothing to log.
            if (stateValues == null && string.IsNullOrEmpty(formattedMessage) && exception == null)
            {
                return;
            }

            // Initialize stateValues so the rest of the methods don't have to worry about null values.
            stateValues = stateValues ?? new Dictionary<string, object>();

            // Add some well-known properties to the scope dictionary so the TelemetryIniitalizer can add them
            // for all telemetry.
            using (BeginScope(new Dictionary<string, object>
            {
                [LogConstants.CategoryNameKey] = _categoryName,
                [LogConstants.LogLevelKey] = (LogLevel?)logLevel,
                [LogConstants.EventIdKey] = eventId.Id
            }))
            {
                // Log a metric from user logs only
                if (_isUserFunction && eventId.Id == LogConstants.MetricEventId)
                {
                    LogMetric(stateValues);
                    return;
                }

                // Log a function result
                if (_categoryName == LogCategories.Results)
                {
                    LogFunctionResult(stateValues, logLevel, exception);
                    return;
                }

                // Log an aggregate record
                if (_categoryName == LogCategories.Aggregator)
                {
                    LogFunctionResultAggregate(stateValues);
                    return;
                }

                // Log an exception
                if (exception != null)
                {
                    LogException(logLevel, stateValues, exception, formattedMessage);
                    return;
                }

                // Otherwise, log a trace
                LogTrace(logLevel, stateValues, formattedMessage);
            }
        }

        private void LogMetric(IEnumerable<KeyValuePair<string, object>> values)
        {
            MetricTelemetry telemetry = new MetricTelemetry();

            // Always apply scope first to allow state to override.
            ApplyScopeProperties(telemetry);

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
                    case LogConstants.NameKey:
                        telemetry.Name = entry.Value.ToString();
                        continue;
                    case LogConstants.MetricValueKey:
                        telemetry.Sum = (double)entry.Value;
                        continue;
                    default:
                        break;
                }

                // Now check for case-insensitive matches
                switch (entry.Key.ToLowerInvariant())
                {
                    case MetricCountKey:
                        telemetry.Count = Convert.ToInt32(entry.Value);
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
                        ApplyProperty(telemetry, entry, LogConstants.CustomPropertyPrefix);
                        break;
                }
            }

            _telemetryClient.TrackMetric(telemetry);
        }

        // Applies scope properties; filters most system properties, which are used internally
        private static void ApplyScopeProperties(ISupportProperties telemetry)
        {
            var scopeProperties = DictionaryLoggerScope.GetMergedStateDictionary();

            var customScopeProperties = scopeProperties.Where(p => !SystemScopeKeys.Contains(p.Key, StringComparer.Ordinal));
            ApplyProperties(telemetry, customScopeProperties, LogConstants.CustomPropertyPrefix);
        }

        private void LogException(LogLevel logLevel, IEnumerable<KeyValuePair<string, object>> values, Exception exception, string formattedMessage)
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
                LogTrace(logLevel, values, formattedMessage);
            }

            ApplyScopeAndStateProperties(telemetry, values);

            _telemetryClient.TrackException(telemetry);
        }

        private void LogTrace(LogLevel logLevel, IEnumerable<KeyValuePair<string, object>> values, string formattedMessage)
        {
            TraceTelemetry telemetry = new TraceTelemetry(formattedMessage)
            {
                SeverityLevel = GetSeverityLevel(logLevel),
                Timestamp = DateTimeOffset.UtcNow
            };

            ApplyScopeAndStateProperties(telemetry, values);

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
        private static void ApplyScopeAndStateProperties(ISupportProperties telemetry, IEnumerable<KeyValuePair<string, object>> state)
        {
            ApplyScopeProperties(telemetry);
            ApplyProperties(telemetry, state, LogConstants.CustomPropertyPrefix);
        }

        private static void ApplyProperty(ISupportProperties telemetry, string key, object value, string propertyPrefix = null)
        {
            // do not apply null values
            if (value == null)
            {
                return;
            }

            string stringValue = null;

            // Format dates
            Type propertyType = value.GetType();
            if (propertyType == typeof(DateTime))
            {
                stringValue = ((DateTime)value).ToUniversalTime().ToString(DateTimeFormatString);
            }
            else if (propertyType == typeof(DateTimeOffset))
            {
                stringValue = ((DateTimeOffset)value).UtcDateTime.ToString(DateTimeFormatString);
            }
            else
            {
                stringValue = value.ToString();
            }

            telemetry.Properties[$"{propertyPrefix}{key}"] = stringValue;
        }

        private static void ApplyProperty(ISupportProperties telemetry, KeyValuePair<string, object> value, string propertyPrefix = null)
        {
            ApplyProperty(telemetry, value.Key, value.Value, propertyPrefix);
        }

        // Inserts properties into the telemetry's properties. Properly formats dates, removes nulls, applies prefix, etc.
        private static void ApplyProperties(ISupportProperties telemetry, IEnumerable<KeyValuePair<string, object>> values, string propertyPrefix = null)
        {
            foreach (var property in values)
            {
                ApplyProperty(telemetry, property.Key, property.Value, propertyPrefix);
            }
        }

        private void LogFunctionResultAggregate(IEnumerable<KeyValuePair<string, object>> values)
        {
            // Metric names will be created like "{FunctionName} {MetricName}"
            IDictionary<string, double> metrics = new Dictionary<string, double>();
            string functionName = LoggingConstants.Unknown;

            // build up the collection of metrics to send
            foreach (KeyValuePair<string, object> value in values)
            {
                switch (value.Key)
                {
                    case LogConstants.NameKey:
                        functionName = value.Value.ToString();
                        break;
                    case LogConstants.TimestampKey:
                    case LogConstants.OriginalFormatKey:
                        // Timestamp is created automatically
                        // We won't use the format string here
                        break;
                    default:
                        if (value.Value is TimeSpan)
                        {
                            // if it's a TimeSpan, log the milliseconds
                            metrics.Add(value.Key, ((TimeSpan)value.Value).TotalMilliseconds);
                        }
                        else if (value.Value is double || value.Value is int)
                        {
                            metrics.Add(value.Key, Convert.ToDouble(value.Value));
                        }

                        // do nothing otherwise
                        break;
                }
            }

            foreach (KeyValuePair<string, double> metric in metrics)
            {
                _telemetryClient.TrackMetric($"{functionName} {metric.Key}", metric.Value);
            }
        }

        private void LogFunctionResult(IEnumerable<KeyValuePair<string, object>> state, LogLevel logLevel, Exception exception)
        {
            IDictionary<string, object> scopeProps = DictionaryLoggerScope.GetMergedStateDictionary() ?? new Dictionary<string, object>();
            // log associated exception details
            KeyValuePair<string, object>[] stateProps = state as KeyValuePair<string, object>[] ?? state.ToArray();
            if (exception != null)
            {
                LogException(logLevel, stateProps, exception, null);
            }

            ApplyFunctionResultActivityTags(stateProps, scopeProps);

            IOperationHolder<RequestTelemetry> requestOperation = scopeProps.GetValueOrDefault<IOperationHolder<RequestTelemetry>>(OperationContext);

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
        private static void ApplyFunctionResultActivityTags(IEnumerable<KeyValuePair<string, object>> state, IDictionary<string, object> scope)
        {
            // Activity carries tracing context. It is managed by instrumented library (e.g. ServiceBus or Asp.Net Core)
            // and consumed by ApplicationInsigts.
            // This function stamps all function-related tags on the Activity. Then WebJobsTelemetryIntitializer sets them on the RequestTelemetry.
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
                        case LogConstants.SucceededKey:
                        case LogConstants.EndTimeKey:
                            // These values are set by the calls to Start/Stop the telemetry. Other
                            // Loggers may want them, but we'll ignore.
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
                if (scope.TryGetValue(LogConstants.CategoryNameKey, out object category))
                {
                    currentActivity.AddTag(LogConstants.CategoryNameKey, category.ToString());
                }

                if (scope.TryGetValue(LogConstants.LogLevelKey, out object logLevel))
                {
                    currentActivity.AddTag(LogConstants.LogLevelKey, logLevel.ToString());
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

            // Http and ServiceBus triggers are tracked automatically by the ApplicationInsights SDK
            // In such case a current Activity is present.
            // We won't track and only stamp function specific details on the RequestTelemtery
            // created by SDK via Activity when function ends
            if (Activity.Current == null)
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
                    RequestTelemetry request = new RequestTelemetry()
                    {
                        Name = functionName
                    };

                    // We'll need to store this operation context so we can stop it when the function completes
                    IOperationHolder<RequestTelemetry> operation = _telemetryClient.StartOperation(request);

#pragma warning disable 612, 618
                    if (_loggerOptions.EnableW3CDistributedTracing)
                    {
                        // currently ApplicationInsights supports 2 parallel correlation schemes:
                        // legacy and W3C, they both appear in telemetry. UX handles all differences in operation Ids. 
                        // This will be resolved in next .NET SDK on Activity level.
                        // This ensures W3C context is set on the Activity.
                        Activity.Current?.GenerateW3CContext();
                    }
#pragma warning restore 612, 618

                    stateValues[OperationContext] = operation;
                }
            }
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

            return httpRequest.HttpContext?.Connection.RemoteIpAddress.ToString() ?? LoggingConstants.ZeroIpAddress;
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
    }
}
