// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
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
            string traceMessage = string.Format(CultureInfo.InvariantCulture, "Executing '{0}' (Reason='{1}', Id={2})", message.Function.ShortName, message.FormatReason(), message.FunctionInstanceId);
            Log(LogLevel.Information, message.Function, message.FunctionInstanceId, traceMessage);

            return Task.FromResult<string>(null);
        }

        public Task LogFunctionCompletedAsync(FunctionCompletedMessage message, CancellationToken cancellationToken)
        {
            if (message.Succeeded)
            {
                string traceMessage = string.Format(CultureInfo.InvariantCulture, "Executed '{0}' (Succeeded, Id={1})", message.Function.ShortName, message.FunctionInstanceId);
                Log(LogLevel.Information, message.Function, message.FunctionInstanceId, traceMessage);
            }
            else
            {
                string traceMessage = string.Format(CultureInfo.InvariantCulture, "Executed '{0}' (Failed, Id={1})", message.Function.ShortName, message.FunctionInstanceId);
                Log(LogLevel.Error, message.Function, message.FunctionInstanceId, traceMessage, message.Failure.Exception);
            }

            return Task.CompletedTask;
        }

        private void Log(LogLevel level, FunctionDescriptor descriptor, Guid functionId, string message, Exception exception = null)
        {
            ILogger logger = _loggerFactory?.CreateLogger(LogCategories.CreateFunctionCategory(descriptor.LogName));
            logger?.Log(level, 0, message, exception, (s, e) => s);
        }

        public Task DeleteLogFunctionStartedAsync(string startedMessageId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
