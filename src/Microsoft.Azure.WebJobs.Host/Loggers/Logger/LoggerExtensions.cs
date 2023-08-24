// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        private static readonly Action<ILogger, int, Exception> _logFunctionRetriesFailed =
           LoggerMessage.Define<int>(
           LogLevel.Error,
           new EventId(324, nameof(LogFunctionRetriesFailed)),
           "Function execution failed after '{attempt}' retries.");

        private static readonly Action<ILogger, TimeSpan, int, int, Exception> _logFunctionRetryAttempt =
           LoggerMessage.Define<TimeSpan, int, int>(
           LogLevel.Debug,
           new EventId(325, nameof(LogFunctionRetryAttempt)),
           "Waiting for `{nextDelay}` before retrying function execution. Next attempt: '{attempt}'. Max retry count: '{retryStrategy.MaxRetryCount}'.");

        private static readonly Action<ILogger, int, string, double, double, Exception> _hostProcessCpuStats =
           LoggerMessage.Define<int, string, double, double>(
           LogLevel.Debug,
           new EventId(326, nameof(HostProcessCpuStats)),
           "[HostMonitor] Host process CPU stats (PID {pid}): History=({formattedCpuLoadHistory}), AvgCpuLoad={avgCpuLoad}, MaxCpuLoad={maxCpuLoad}");

        private static readonly Action<ILogger, double, float, Exception> _hostCpuThresholdExceeded =
           LoggerMessage.Define<double, float>(
           LogLevel.Warning,
           new EventId(327, nameof(HostCpuThresholdExceeded)),
           "[HostMonitor] Host CPU threshold exceeded ({aggregateCpuLoad} >= {cpuThreshold})");

        private static readonly Action<ILogger, double, Exception> _hostAggregateCpuLoad =
           LoggerMessage.Define<double>(
           LogLevel.Debug,
           new EventId(328, nameof(HostAggregateCpuLoad)),
           "[HostMonitor] Host aggregate CPU load {aggregateCpuLoad}");

        private static readonly Action<ILogger, int, string, double, double, Exception> _hostProcessMemoryUsage =
           LoggerMessage.Define<int, string, double, double>(
           LogLevel.Debug,
           new EventId(329, nameof(HostProcessMemoryUsage)),
           "[HostMonitor] Host process memory usage (PID {pid}): History=({formattedMemoryUsageHistory}), AvgUsage={avgMemoryUsage}, MaxUsage={maxMemoryUsage}");

        private static readonly Action<ILogger, double, double, Exception> _hostMemoryThresholdExceeded =
           LoggerMessage.Define<double, double>(
           LogLevel.Warning,
           new EventId(330, nameof(HostMemoryThresholdExceeded)),
           "[HostMonitor] Host memory threshold exceeded ({aggregateMemoryUsage} >= {memoryThreshold})");

        private static readonly Action<ILogger, double, int, Exception> _hostAggregateMemoryUsage =
           LoggerMessage.Define<double, int>(
           LogLevel.Debug,
           new EventId(331, nameof(HostAggregateMemoryUsage)),
           "[HostMonitor] Host aggregate memory usage {aggregateMemoryUsage} ({percentageOfMax}% of threshold)");

        private static readonly Action<ILogger, string, int, int, Exception> _hostConcurrencyStatus =
           LoggerMessage.Define<string, int, int>(
           LogLevel.Debug,
           new EventId(332, nameof(HostConcurrencyStatus)),
           "{functionId} Concurrency: {concurrency}, OutstandingInvocations: {outstandingInvocations}");

        private static readonly Action<ILogger, Exception> _hostThreadStarvation =
           LoggerMessage.Define(
           LogLevel.Warning,
           new EventId(333, nameof(HostThreadStarvation)),
           "Possible thread pool starvation detected.");

        private static readonly Action<ILogger, string, Exception> _primaryHostCoordinatorFailedToRenewLockLease =
            LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(334, nameof(PrimaryHostCoordinatorFailedToRenewLockLease)),
            "Failed to renew host lock lease: {reason}");

        private static readonly Action<ILogger, string, string, Exception> _primaryHostCoordinatorFailedToAcquireLockLease =
            LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(335, nameof(PrimaryHostCoordinatorFailedToAcquireLockLease)),
            "Host instance '{websiteInstanceId}' failed to acquire host lock lease: {reason}");

        private static readonly Action<ILogger, string, Exception> _primaryHostCoordinatorReleasedLocklLease =
            LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(336, nameof(PrimaryHostCoordinatorReleasedLocklLease)),
            "Host instance '{websiteInstanceId}' released lock lease.");

        private static readonly Action<ILogger, string, Exception> _primaryHostCoordinatorLockLeaseAcquired =
            LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(337, nameof(PrimaryHostCoordinatorLockLeaseAcquired)),
            "Host lock lease acquired by instance ID '{websiteInstanceId}'.");

        private static readonly Action<ILogger, string, string, Exception> _functionConcurrencyDecrease =
           LoggerMessage.Define<string, string>(
           LogLevel.Debug,
           new EventId(338, nameof(FunctionConcurrencyDecrease)),
           "{functionId} Decreasing concurrency (Enabled throttles: {enabledThrottles})");

        private static readonly Action<ILogger, string, Exception> _functionConcurrencyIncrease =
           LoggerMessage.Define<string>(
           LogLevel.Debug,
           new EventId(339, nameof(FunctionConcurrencyIncrease)),
           "{functionId} Increasing concurrency");

        private static readonly Action<ILogger, Exception> _logExitFromRetryLoop =
           LoggerMessage.Define(
           LogLevel.Warning,
           new EventId(340, nameof(LogExitFromRetryLoop)),
           "Invocation cancelled - exiting retry loop.");

        public static void PrimaryHostCoordinatorLockLeaseAcquired(this ILogger logger, string websiteInstanceId)
        {
            _primaryHostCoordinatorLockLeaseAcquired(logger, websiteInstanceId, null);
        }

        public static void PrimaryHostCoordinatorFailedToRenewLockLease(this ILogger logger, string reason)
        {
            _primaryHostCoordinatorFailedToRenewLockLease(logger, reason, null);
        }

        public static void PrimaryHostCoordinatorFailedToAcquireLockLease(this ILogger logger, string websiteInstanceId, string reason)
        {
            _primaryHostCoordinatorFailedToAcquireLockLease(logger, websiteInstanceId, reason, null);
        }

        public static void PrimaryHostCoordinatorReleasedLocklLease(this ILogger logger, string websiteInstanceId)
        {
            _primaryHostCoordinatorReleasedLocklLease(logger, websiteInstanceId, null);
        }

        public static void HostThreadStarvation(this ILogger logger)
        {
            _hostThreadStarvation(logger, null);
        }

        public static void HostConcurrencyStatus(this ILogger logger, string functionId, int concurrency, int outstandingInvocations)
        {
            _hostConcurrencyStatus(logger, functionId, concurrency, outstandingInvocations, null);
        }

        public static void HostAggregateCpuLoad(this ILogger logger, double aggregateCpuLoad)
        {
            _hostAggregateCpuLoad(logger, aggregateCpuLoad, null);
        }

        public static void HostProcessCpuStats(this ILogger logger, int pid, string formattedCpuLoadHistory, double avgCpuLoad, double maxCpuLoad)
        {
            _hostProcessCpuStats(logger, pid, formattedCpuLoadHistory, avgCpuLoad, maxCpuLoad, null);
        }

        public static void HostCpuThresholdExceeded(this ILogger logger, double aggregateCpuLoad, float cpuThreshold)
        {
            _hostCpuThresholdExceeded(logger, aggregateCpuLoad, cpuThreshold, null);
        }

        public static void HostAggregateMemoryUsage(this ILogger logger, double aggregateMemoryUsage, int percentageOfMax)
        {
            _hostAggregateMemoryUsage(logger, aggregateMemoryUsage, percentageOfMax, null);
        }

        public static void HostProcessMemoryUsage(this ILogger logger, int pid, string formattedMemoryUsageHistory, double avgMemoryUsage, double maxMemoryUsage)
        {
            _hostProcessMemoryUsage(logger, pid, formattedMemoryUsageHistory, avgMemoryUsage, maxMemoryUsage, null);
        }

        public static void HostMemoryThresholdExceeded(this ILogger logger, double aggregateMemoryUsage, double memoryThreshold)
        {
            _hostMemoryThresholdExceeded(logger, aggregateMemoryUsage, memoryThreshold, null);
        }

        public static void FunctionConcurrencyDecrease(this ILogger logger, string functionId, string enabledThrottles)
        {
            _functionConcurrencyDecrease(logger, functionId, enabledThrottles, null);
        }

        public static void FunctionConcurrencyIncrease(this ILogger logger, string functionId)
        {
            _functionConcurrencyIncrease(logger, functionId, null);
        }

        /// <summary>
        /// Logs a metric value.
        /// </summary>
        /// <param name="logger">The ILogger.</param>
        /// <param name="name">The name of the metric.</param>
        /// <param name="value">The value of the metric.</param>
        /// <param name="properties">Named string values for classifying and filtering metrics.</param>
        public static void LogMetric(this ILogger logger, string name, double value, IDictionary<string, object> properties = null)
        {
            logger?.Log(
                LogLevel.Information,
                LogConstants.MetricEventId,
                new MetricState(name, value, properties),
                null,
                static (s, e) => null);
        }

        internal static void LogFunctionResult(this ILogger logger, FunctionInstanceLogEntry logEntry)
        {
            bool succeeded = logEntry.Exception == null;

            LogLevel level = succeeded ? LogLevel.Information : LogLevel.Error;

            logger.Log(
                level,
                0,
                new FunctionResultState(logEntry, succeeded),
                logEntry.Exception,
                static (s, e) => null);
        }

        internal static void LogFunctionResultAggregate(this ILogger logger, FunctionResultAggregate resultAggregate)
        {
            logger.Log(
                LogLevel.Information,
                0,
                new FunctionResultAggregateState(resultAggregate),
                null,
                static (s, e) => null);
        }

        internal static IDisposable BeginFunctionScope(this ILogger logger, IFunctionInstance functionInstance, Guid hostInstanceId)
        {
            return logger?.BeginScope(
                new Dictionary<string, object>(5)
                {
                    [ScopeKeys.FunctionInvocationId] = functionInstance?.Id.ToString(),
                    [ScopeKeys.FunctionName] = functionInstance?.FunctionDescriptor?.LogName,
                    [ScopeKeys.Event] = LogConstants.FunctionStartEvent,
                    [ScopeKeys.HostInstanceId] = hostInstanceId.ToString(),
                    [ScopeKeys.TriggerDetails] = functionInstance?.TriggerDetails
                });
        }

        public static void LogFunctionRetryAttempt(this ILogger logger, TimeSpan nextDelay, int attemptCount, int maxRetryCount)
        {
            _logFunctionRetryAttempt(logger, nextDelay, attemptCount, maxRetryCount, null);
        }

        public static void LogFunctionRetriesFailed(this ILogger logger, int attemptCount, IDelayedException result)
        {
            _logFunctionRetriesFailed(logger, attemptCount, result?.Exception);
        }

        internal static void LogExitFromRetryLoop(this ILogger logger)
        {
            _logExitFromRetryLoop(logger, null);
        }
    }
}
