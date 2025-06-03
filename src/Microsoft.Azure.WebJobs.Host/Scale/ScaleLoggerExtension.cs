using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    public static class ScaleLoggerExtension
    {
        private static readonly EventId FunctionScaleErrorEventId = new EventId(8001, "FunctionScaleError");
        private static readonly EventId LogFunctionScaleVoteEventId = new EventId(8002, "LogFunctionScaleVote");
        private static readonly string FunctionNameKey = "functionName";
        private static readonly string TargetWorkerCountKey = "targetWorkerCount";
        private static readonly string QueueLengthKey = "queueLength";
        private static readonly string ConcurrencyKey = "concurrency";
        private static readonly string VoteKey = "vote";
        private static readonly string ReasonKey = "reason"; // Renamed from 'Reason'

        // <summary>
        // Logs a scale vote for a function with the specified name, vote, and optional reason.
        // </summary>
        public static void LogFunctionScaleVote(this ILogger logger, string functionName, string vote, string reason = null)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                [FunctionNameKey] = functionName,
                [VoteKey] = vote,
                [ReasonKey] = reason
            }))
            {
                logger.LogDebug(
                    LogFunctionScaleVoteEventId,
                    "Function '{functionName}' vote: '{vote}'.", // Fixed typo
                    functionName,
                    vote);
            }
        }

        // <summary>
        // Logs a scale vote for a function with the specified name, target worker count, queue length, concurrency, and optional reason.
        // </summary>
        public static void LogFunctionScaleVote(this ILogger logger, string functionName, int targetWorkerCount, int queueLength, int concurrency, string reason = null)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                [FunctionNameKey] = functionName,
                [TargetWorkerCountKey] = targetWorkerCount,
                [QueueLengthKey] = queueLength,
                [ConcurrencyKey] = concurrency,
                [ReasonKey] = reason
            }))
            {
                logger.LogDebug(
                    LogFunctionScaleVoteEventId,
                    "Function '{functionName}' vote: TargetWorkerCount='{targetWorkerCount}', QueueLength='{queueLength}', Concurrency='{concurrency}'.",
                    functionName,
                    targetWorkerCount,
                    queueLength,
                    concurrency);
            }
        }

        /// <summary>
        /// Logs an error that occurred while scaling a function, including the function name and exception details.
        /// </summary>
        internal static void LogFunctionScaleError(this ILogger logger, string message, string functionName, Exception ex)
        {
            using (logger.BeginScope(new Dictionary<string, object> { [FunctionNameKey] = functionName }))
            {
                logger.LogError(
                    FunctionScaleErrorEventId,
                    ex,
                    "Function '{functionName}' error: {message}",
                    functionName,
                    message);
            }
        }

        /// <summary>
        /// Logs a vote for the target worker count of a function.
        /// </summary>
        internal static void LogFunctionScaleVote(this ILogger logger, string functionName, int targetWorkerCount)
        {
            using (logger.BeginScope(new Dictionary<string, object> { [FunctionNameKey] = functionName, [TargetWorkerCountKey] = targetWorkerCount }))
            {
                logger.LogDebug(
                LogFunctionScaleVoteEventId,
                "Function '{functionName}' vote: TargetWorkerCount='{targetWorkerCount}'.",
                functionName,
                targetWorkerCount);
            }
        }
    }
}
