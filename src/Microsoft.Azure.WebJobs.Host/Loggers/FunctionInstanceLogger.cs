// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
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

        private readonly ConcurrentDictionary<string, ILogger> _loggers = new ConcurrentDictionary<string, ILogger>();

        private static readonly Action<ILogger, string, string, Guid, Exception> LogFunctionStarted =
            LoggerMessage.Define<string, string, Guid>(LogLevel.Information, 0, "Executing '{Function}' (Reason='{Reason}', Id={Id})");

        private static readonly Action<ILogger, string, Guid, Exception> LogFunctionCompletedSuccess =
            LoggerMessage.Define<string, Guid>(LogLevel.Information, 0, "Executed '{Function}' (Succeeded, Id={Id})");

        private static readonly Action<ILogger, string, Guid, Exception> LogFunctionCompletedFailure =
            LoggerMessage.Define<string, Guid>(LogLevel.Error, 0, "Executed '{Function}' (Failed, Id={Id})");

        private static readonly Task<string> StringNullTask = Task.FromResult<string>(null);

        public FunctionInstanceLogger(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public Task<string> LogFunctionStartedAsync(FunctionStartedMessage message, CancellationToken cancellationToken)
        {
            var logger = GetLogger(message.Function);
            LogFunctionStarted(logger, message.Function.ShortName, message.FormatReason(), message.FunctionInstanceId, null);

            if (message.TriggerDetails != null && message.TriggerDetails.Count != 0)
            {
                LogTemplatizedTriggerDetails(message);
            }

            return StringNullTask;
        }

        private void LogTemplatizedTriggerDetails(FunctionStartedMessage message)
        {
            // TODO: cache trigger template
            var templateKeys = message.TriggerDetails.Select(entry => $"{entry.Key}: {{{entry.Key}}}");
            string messageTemplate = "Trigger Details: " + string.Join(", ", templateKeys);
            string[] templateValues = message.TriggerDetails.Values.ToArray();

            ILogger logger = GetLogger(message.Function);
            logger?.Log(LogLevel.Information, 0, messageTemplate, templateValues);
        }

        public Task LogFunctionCompletedAsync(FunctionCompletedMessage message, CancellationToken cancellationToken)
        {
            ILogger logger = GetLogger(message.Function);

            if (message.Succeeded)
            {
                LogFunctionCompletedSuccess(logger, message.Function.ShortName, message.FunctionInstanceId, null);
            }
            else
            {
                LogFunctionCompletedFailure(logger, message.Function.ShortName, message.FunctionInstanceId, message.Failure.Exception);
            }

            return Task.CompletedTask;
        }

        private ILogger GetLogger(FunctionDescriptor descriptor)
        {
            return _loggers.GetOrAdd(descriptor.LogName, logName => _loggerFactory?.CreateLogger(LogCategories.CreateFunctionCategory(logName)));
        }

        public Task DeleteLogFunctionStartedAsync(string startedMessageId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
