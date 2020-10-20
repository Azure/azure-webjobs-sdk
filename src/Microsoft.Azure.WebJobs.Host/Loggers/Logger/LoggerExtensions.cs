// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Logging;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extension methods for use with <see cref="ILogger"/>.
    /// </summary>
    public static class LoggerExtensions
    {
        private static readonly Action<ILogger, TimeSpan, int, int, Exception> _logFunctionRetryAttempt =
            LoggerMessage.Define<TimeSpan, int, int>(
                LogLevel.Debug,
                new EventId(325, nameof(LogFunctionRetryAttempt)),
                "Waiting for `{nextDelay}` before retrying function execution. Next attempt: '{attempt}'. Max retry count: '{retryStrategy.MaxRetryCount}'");

        /// <summary>
        /// Logs a metric value.
        /// </summary>
        /// <param name="logger">The ILogger.</param>
        /// <param name="name">The name of the metric.</param>
        /// <param name="value">The value of the metric.</param>
        /// <param name="properties">Named string values for classifying and filtering metrics.</param>
        public static void LogMetric(this ILogger logger, string name, double value,
            IDictionary<string, object> properties = null)
        {
            IDictionary<string, object> state = properties == null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(properties);

            state[LogConstants.NameKey] = name;
            state[LogConstants.MetricValueKey] = value;

            IDictionary<string, object> payload = new ReadOnlyDictionary<string, object>(state);
            logger?.Log(LogLevel.Information, LogConstants.MetricEventId, payload, null, (s, e) => null);
        }

        private static readonly string[] FunctionResultKeys = 
        {
            LogConstants.FullNameKey,
            LogConstants.InvocationIdKey,
            LogConstants.NameKey,
            LogConstants.TriggerReasonKey,
            LogConstants.StartTimeKey,
            LogConstants.EndTimeKey,
            LogConstants.DurationKey,
            LogConstants.SucceededKey,
        };

        internal static void LogFunctionResult(this ILogger logger, FunctionInstanceLogEntry logEntry)
        {
            bool succeeded = logEntry.Exception == null;

            IReadOnlyDictionary<string, object> payload = new ReadOnlyScopeDictionary(FunctionResultKeys, new object[]
            {
                logEntry.FunctionName,
                logEntry.FunctionInstanceId,
                logEntry.LogName,
                logEntry.TriggerReason,
                logEntry.StartTime,
                logEntry.EndTime,
                logEntry.Duration,
                succeeded
            });
            
            LogLevel level = succeeded ? LogLevel.Information : LogLevel.Error;

            // Only pass the state dictionary; no string message.
            logger.Log(level, 0, payload, logEntry.Exception, (s, e) => null);
        }

        internal static void LogFunctionResultAggregate(this ILogger logger, FunctionResultAggregate resultAggregate)
        {
            // we won't output any string here, just the data
            logger.Log(LogLevel.Information, 0, resultAggregate.ToReadOnlyDictionary(), null, (s, e) => null);
        }

        internal static IDisposable BeginFunctionScope(this ILogger logger, IFunctionInstance functionInstance, Guid hostInstanceId)
        {
            return logger?.BeginScope(
                new ReadOnlyScopeDictionary(BeginScopeKeys, new object[]
                {
                    functionInstance?.Id.ToString(),
                    functionInstance?.FunctionDescriptor?.LogName,
                    LogConstants.FunctionStartEvent,
                    hostInstanceId.ToString(),
                    functionInstance?.TriggerDetails
                }));
        }

        public static void LogFunctionRetryAttempt(this ILogger logger, TimeSpan nextDelay, int attemptCount, int maxRetryCount)
        {
            _logFunctionRetryAttempt(logger, nextDelay, attemptCount, maxRetryCount, null);
        }

        private static readonly string[] BeginScopeKeys =
        {
            ScopeKeys.FunctionInvocationId,
            ScopeKeys.FunctionName,
            ScopeKeys.Event,
            ScopeKeys.HostInstanceId,
            ScopeKeys.TriggerDetails
        };
    }
}