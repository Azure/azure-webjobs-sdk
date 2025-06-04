// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    public static partial class ScaleLoggerExtension
    {
        private static readonly EventId FunctionScaleErrorEventId = new EventId(8001, "FunctionScaleError");
        private static readonly EventId LogFunctionScaleVoteEventId = new EventId(8002, "LogFunctionScaleVote");

        // High-performance logging delegates
        private static readonly Action<ILogger, string, string, Exception> _logFunctionScaleVoteSimple =
            LoggerMessage.Define<string, string>(
                LogLevel.Debug,
                LogFunctionScaleVoteEventId,
                "Function '{functionName}' vote: '{vote}'.");

        private static readonly Action<ILogger, string, string, string, Exception> _logFunctionScaleVoteSimpleWithReason =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Debug,
                LogFunctionScaleVoteEventId,
                "Function '{functionName}' vote: '{vote}'. {reason}");

        private static readonly Action<ILogger, string, int, int, int, Exception> _logFunctionScaleVoteDetailed =
            LoggerMessage.Define<string, int, int, int>(
                LogLevel.Debug,
                LogFunctionScaleVoteEventId,
                "Function '{functionName}' vote: TargetWorkerCount='{targetWorkerCount}', QueueLength='{queueLength}', Concurrency='{concurrency}'.");

        private static readonly Action<ILogger, string, int, int, int, string, Exception> _logFunctionScaleVoteDetailedWithReason =
            LoggerMessage.Define<string, int, int, int, string>(
                LogLevel.Debug,
                LogFunctionScaleVoteEventId,
                "Function '{functionName}' vote: TargetWorkerCount='{targetWorkerCount}', QueueLength='{queueLength}', Concurrency='{concurrency}'. {reason}");

        private static readonly Action<ILogger, string, int, Exception> _logFunctionScaleVoteTargetWorkerCount =
            LoggerMessage.Define<string, int>(
                LogLevel.Debug,
                LogFunctionScaleVoteEventId,
                "Function '{functionName}' vote: TargetWorkerCount='{targetWorkerCount}'.");

        private static readonly Action<ILogger, string, string, Exception> _logFunctionScaleError =
            LoggerMessage.Define<string, string>(
                LogLevel.Error,
                FunctionScaleErrorEventId,
                "Function '{functionName}' error: {message}");

        // <summary>
        // Logs a scale vote for a function with the specified name, vote, and optional reason.
        // </summary>
        public static void LogFunctionScaleVote(this ILogger logger, string functionName, string vote, string reason = null)
        {
            if (string.IsNullOrEmpty(reason))
            {
                _logFunctionScaleVoteSimple(logger, functionName, vote, null);
            }
            else
            {
                _logFunctionScaleVoteSimpleWithReason(logger, functionName, vote, reason, null);
            }
        }

        // <summary>
        // Logs a scale vote for a function with the specified name, target worker count, queue length, concurrency, and optional reason.
        // </summary>
        public static void LogFunctionScaleVote(this ILogger logger, string functionName, int targetWorkerCount, int queueLength, int concurrency, string reason = null)
        {
            if (string.IsNullOrEmpty(reason))
            {
                _logFunctionScaleVoteDetailed(logger, functionName, targetWorkerCount, queueLength, concurrency, null);
            }
            else
            {
                _logFunctionScaleVoteDetailedWithReason(logger, functionName, targetWorkerCount, queueLength, concurrency, reason, null);
            }
        }

        /// <summary>
        /// Logs an error that occurred while scaling a function, including the function name and exception details.
        /// </summary>
        internal static void LogFunctionScaleError(this ILogger logger, string message, string functionName, Exception ex)
        {
            _logFunctionScaleError(logger, functionName, message, ex);
        }

        /// <summary>
        /// Logs a vote for the target worker count of a function.
        /// </summary>
        internal static void LogFunctionScaleVote(this ILogger logger, string functionName, int targetWorkerCount)
        {
            _logFunctionScaleVoteTargetWorkerCount(logger, functionName, targetWorkerCount, null);
        }
    }
}
