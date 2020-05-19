// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal class FunctionInstanceLogger : IFunctionInstanceLogger
    {
        private readonly ILoggerFactory _loggerFactory;

        public FunctionInstanceLogger(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public Task<string> LogFunctionStartedAsync(FunctionStartedMessage message, CancellationToken cancellationToken)
        {
            var logger = GetLogger(message.Function);
            Log.FunctionStarted(
                logger, 
                message.Function.ShortName, 
                message.ReasonDetails ?? message.FormatReason(), 
                message.FunctionInstanceId);

            if (message.TriggerDetails != null && message.TriggerDetails.Count != 0)
            {
                LogTemplatizedTriggerDetails(logger, message);
            }

            return Task.FromResult<string>(null);
        }

        private void LogTemplatizedTriggerDetails(ILogger logger, FunctionStartedMessage message)
        {
            var templateKeys = message.TriggerDetails.Select(entry => $"{entry.Key}: {{{entry.Key}}}");
            string messageTemplate = "Trigger Details: " + string.Join(", ", templateKeys);
            string[] templateValues = message.TriggerDetails.Values.ToArray();

            logger.LogInformation(messageTemplate, templateValues);
        }

        public Task LogFunctionCompletedAsync(FunctionCompletedMessage message, CancellationToken cancellationToken)
        {
            var logger = GetLogger(message.Function);
            Log.FunctionCompleted(
                logger, 
                message.Function.ShortName, 
                message.FunctionInstanceId,
                message.Succeeded,
                message.Failure);

            return Task.CompletedTask;
        }

        public Task DeleteLogFunctionStartedAsync(string startedMessageId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private ILogger GetLogger(FunctionDescriptor descriptor)
        {
            return _loggerFactory?.CreateLogger(LogCategories.CreateFunctionCategory(descriptor.LogName));
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, string, Guid, Exception> _functionStarted =
                LoggerMessage.Define<string, string, Guid>(
                    LogLevel.Information,
                    new EventId(1, nameof(FunctionStarted)),
                    "Executing '{functionName}' (Reason='{reason}', Id={invocationId})");

            private static readonly Action<ILogger, string, string, Guid, Exception> _functionSucceeded =
                LoggerMessage.Define<string, string, Guid>(
                    LogLevel.Information,
                    new EventId(2, nameof(FunctionCompleted)),
                    "Executed '{functionName}' ({status}, Id={invocationId})");

            private static readonly Action<ILogger, string,  string, Guid, Exception> _functionFailed =
                LoggerMessage.Define<string, string, Guid>(
                    LogLevel.Error,
                    new EventId(3, nameof(FunctionCompleted)),
                    "Executed '{functionName}' ({status}, Id={invocationId})");

            public static void FunctionStarted(ILogger logger, string functionName, string reason, Guid invocationId)
            {
                _functionStarted(logger, functionName, reason, invocationId, null);
            }

            public static void FunctionCompleted(ILogger logger, string functionName, Guid invocationId, bool succeeded, FunctionFailure failure)
            {
                string status = succeeded ? "Succeeded" : "Failed";
                if (succeeded)
                {
                    _functionSucceeded(logger, functionName, status, invocationId, null);
                }
                else
                {
                    _functionFailed(logger, functionName, status, invocationId, failure?.Exception);
                }
            }
        }
    }
}
