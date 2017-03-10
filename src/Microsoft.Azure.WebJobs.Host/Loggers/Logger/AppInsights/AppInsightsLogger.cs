// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal class AppInsightsLogger : ILogger
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly string _categoryName;
        private const string DefaultCategoryName = "Default";
        private const string DateTimeFormatString = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK";
        private Func<string, LogLevel, bool> _filter;

        public AppInsightsLogger(TelemetryClient client, string categoryName, Func<string, LogLevel, bool> filter)
        {
            _telemetryClient = client;
            _categoryName = categoryName ?? DefaultCategoryName;
            _filter = filter;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            IEnumerable<KeyValuePair<string, object>> stateValues = state as IEnumerable<KeyValuePair<string, object>>;

            // We only support lists of key-value pairs. Anything else we'll skip.
            if (stateValues == null)
            {
                return;
            }

            string formattedMessage = formatter(state, exception);

            // Log a function result
            if (_categoryName == LoggingCategories.Results)
            {
                LogFunctionResult(stateValues, exception);
                return;
            }

            // Log an aggregate record
            if (_categoryName == LoggingCategories.Aggregator)
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

        private void LogException(LogLevel logLevel, IEnumerable<KeyValuePair<string, object>> values, Exception exception, string formattedMessage)
        {
            ExceptionTelemetry telemetry = new ExceptionTelemetry(exception);
            telemetry.Message = formattedMessage;
            telemetry.Timestamp = DateTimeOffset.UtcNow;
            ApplyScopeProperties(telemetry);
            ApplyCustomProperties(telemetry, logLevel, values);
            _telemetryClient.TrackException(telemetry);
        }

        private void LogTrace(LogLevel logLevel, IEnumerable<KeyValuePair<string, object>> values, string formattedMessage)
        {
            TraceTelemetry telemetry = new TraceTelemetry(formattedMessage);
            telemetry.Timestamp = DateTimeOffset.UtcNow;
            ApplyScopeProperties(telemetry);
            ApplyCustomProperties(telemetry, logLevel, values);
            _telemetryClient.TrackTrace(telemetry);
        }

        private void ApplyCustomProperties(ISupportProperties telemetry, LogLevel logLevel, IEnumerable<KeyValuePair<string, object>> values)
        {
            telemetry.Properties.Add(LoggingKeys.CategoryName, _categoryName);
            telemetry.Properties.Add(LoggingKeys.Level, logLevel.ToString());

            foreach (var property in values)
            {
                string stringValue = null;

                // Format dates
                Type propertyType = property.Value?.GetType();
                if (propertyType == typeof(DateTime))
                {
                    stringValue = ((DateTime)property.Value).ToString(DateTimeFormatString);
                }
                else if (propertyType == typeof(DateTimeOffset))
                {
                    stringValue = ((DateTimeOffset)property.Value).UtcDateTime.ToString(DateTimeFormatString);
                }
                else
                {
                    stringValue = property.Value?.ToString();
                }

                // Since there is no nesting of properties, apply a prefix before the property name to lessen
                // the chance of collisions.
                telemetry.Properties.Add(LoggingKeys.CustomPropertyPrefix + property.Key, stringValue);
            }
        }

        private void LogFunctionResultAggregate(IEnumerable<KeyValuePair<string, object>> values)
        {
            IDictionary<string, double> metrics = new Dictionary<string, double>();
            string functionName = "[Unknown]";

            foreach (KeyValuePair<string, object> value in values)
            {
                switch (value.Key)
                {
                    case LoggingKeys.Name:
                        functionName = value.Value.ToString();
                        break;
                    case LoggingKeys.Timestamp:
                    case LoggingKeys.OriginalFormat:
                        // Timestamp is created automatically
                        // We won't use the format string here
                        break;
                    default:
                        // try to insert the value as a metric
                        if (value.Value is double || value.Value is int)
                        {
                            metrics.Add(value.Key, Convert.ToDouble(value.Value));
                        }

                        // do nothing otherwise
                        break;
                }
            }

            _telemetryClient.TrackEvent(functionName + ".Aggregation", metrics: metrics);
        }

        private void LogFunctionResult(IEnumerable<KeyValuePair<string, object>> values, Exception exception)
        {
            IDictionary<string, object> scopeProps = AppInsightsScope.Current?.GetMergedStateDictionary() ?? new Dictionary<string, object>();

            RequestTelemetry requestTelemetry = new RequestTelemetry();
            requestTelemetry.Success = exception == null;
            requestTelemetry.ResponseCode = "0";

            // Set ip address to zeroes. If we find HttpRequest details below, we will update this
            requestTelemetry.Context.Location.Ip = "0.0.0.0";

            ApplyFunctionResultProperties(requestTelemetry, values);

            // Functions attaches the HttpRequest, which allows us to log richer request details.
            object request;
            if (scopeProps.TryGetValue(ScopeKeys.HttpRequest, out request) &&
                request is HttpRequestMessage)
            {
                ApplyHttpRequestProperties(requestTelemetry, (HttpRequestMessage)request);
            }

            // log associated exception details
            if (exception != null)
            {
                ExceptionTelemetry exceptionTelemetry = new ExceptionTelemetry(exception);

                string invocationId = values.Where(v => v.Key == LoggingKeys.InvocationId).Select(v => v.Value.ToString()).SingleOrDefault();
                string functionName = values.Where(v => v.Key == LoggingKeys.Name).Select(v => v.Value.ToString()).SingleOrDefault();

                exceptionTelemetry.Context.Operation.Id = invocationId;
                exceptionTelemetry.Context.Operation.Name = functionName;

                _telemetryClient.TrackException(exceptionTelemetry);
            }

            _telemetryClient.TrackRequest(requestTelemetry);
        }

        private static void ApplyHttpRequestProperties(RequestTelemetry requestTelemetry, HttpRequestMessage request)
        {
            requestTelemetry.Url = request.RequestUri;
            requestTelemetry.Properties[LoggingKeys.HttpMethod] = request.Method.ToString();

            requestTelemetry.Context.Location.Ip = GetIpAddress(request);
            requestTelemetry.Context.User.UserAgent = request.Headers.UserAgent?.ToString();

            HttpResponseMessage response = GetResponse(request);
            requestTelemetry.ResponseCode = ((int)response?.StatusCode).ToString();
        }

        private static void ApplyFunctionResultProperties(RequestTelemetry requestTelemetry, IEnumerable<KeyValuePair<string, object>> stateValues)
        {
            // Build up the telemetry model. Some values special and go right on the telemetry object. All others
            // are added to the Properties bag.
            foreach (KeyValuePair<string, object> prop in stateValues)
            {
                switch (prop.Key)
                {
                    case LoggingKeys.Name:
                        requestTelemetry.Name = prop.Value.ToString();
                        requestTelemetry.Context.Operation.Name = prop.Value.ToString();
                        break;
                    case LoggingKeys.InvocationId:
                        requestTelemetry.Id = prop.Value.ToString();
                        requestTelemetry.Context.Operation.Id = prop.Value.ToString();
                        break;
                    case LoggingKeys.StartTime:
                        DateTimeOffset startTime = new DateTimeOffset((DateTime)prop.Value, TimeSpan.Zero);
                        requestTelemetry.Timestamp = startTime;
                        requestTelemetry.Properties.Add(prop.Key, startTime.ToString(DateTimeFormatString));
                        break;
                    case LoggingKeys.Duration:
                        requestTelemetry.Duration = TimeSpan.Parse(prop.Value.ToString());
                        break;
                    case LoggingKeys.OriginalFormat:
                        // this is the format string; we won't use it here
                        break;
                    default:
                        if (prop.Value is DateTime)
                        {
                            DateTimeOffset date = new DateTimeOffset((DateTime)prop.Value, TimeSpan.Zero);
                            requestTelemetry.Properties.Add(prop.Key, date.ToString(DateTimeFormatString));
                        }
                        else
                        {
                            requestTelemetry.Properties.Add(prop.Key, prop.Value?.ToString());
                        }
                        break;
                }
            }
        }

        private static void ApplyScopeProperties(ITelemetry telemetryModel)
        {
            IDictionary<string, object> scopeProps = AppInsightsScope.Current?.GetMergedStateDictionary() ?? new Dictionary<string, object>();

            object functionInvocationId;
            if (scopeProps.TryGetValue(ScopeKeys.FunctionInvocationId, out functionInvocationId))
            {
                telemetryModel.Context.Operation.Id = functionInvocationId.ToString();
            }

            object functionName;
            if (scopeProps.TryGetValue(ScopeKeys.FunctionName, out functionName) &&
                functionName is string)
            {
                telemetryModel.Context.Operation.Name = (string)functionName;
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            if (_filter == null)
            {
                return true;
            }

            return _filter(_categoryName, logLevel);
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return AppInsightsScope.Push(state);
        }

        internal static string GetIpAddress(HttpRequestMessage httpRequest)
        {
            string address = null;
            object context;
            if (httpRequest.Properties.TryGetValue(ScopeKeys.HttpContext, out context) &&
                context is HttpContextBase)
            {
                HttpContextBase contextBase = (HttpContextBase)context;
                address = contextBase.Request?.UserHostAddress ?? "0.0.0.0";
            }
            return address;
        }

        internal static HttpResponseMessage GetResponse(HttpRequestMessage httpRequest)
        {
            // Grab the response stored by functions
            HttpResponseMessage httpResponse = null;
            object response;
            if (httpRequest.Properties.TryGetValue(ScopeKeys.FunctionsHttpResponse, out response) &&
                response is HttpResponseMessage)
            {
                httpResponse = (HttpResponseMessage)response;
            }
            return httpResponse;
        }
    }
}
