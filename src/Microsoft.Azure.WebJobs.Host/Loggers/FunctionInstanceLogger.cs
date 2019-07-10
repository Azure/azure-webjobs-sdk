// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        private readonly ConcurrentDictionary<string, Func<ILogger, IDictionary<string, string>, bool>> _triggerLoggers = new ConcurrentDictionary<string, Func<ILogger, IDictionary<string, string>, bool>>();

        private static readonly Action<ILogger, string, string, Guid, Exception> FunctionStarted =
            LoggerMessage.Define<string, string, Guid>(LogLevel.Information, new EventId(1000, nameof(FunctionStarted)), "Executing '{Function}' (Reason='{Reason}', Id={Id})");

        private static readonly Action<ILogger, string, Guid, Exception> FunctionCompletedSuccess =
            LoggerMessage.Define<string, Guid>(LogLevel.Information, new EventId(1001, nameof(FunctionCompletedSuccess)), "Executed '{Function}' (Succeeded, Id={Id})");

        private static readonly Action<ILogger, string, Guid, Exception> FunctionCompletedFailure =
            LoggerMessage.Define<string, Guid>(LogLevel.Error, new EventId(1002, nameof(FunctionCompletedFailure)), "Executed '{Function}' (Failed, Id={Id})");

        private static readonly Task<string> StringNullTask = Task.FromResult<string>(null);

        public FunctionInstanceLogger(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public Task<string> LogFunctionStartedAsync(FunctionStartedMessage message, CancellationToken cancellationToken)
        {
            var logger = GetLogger(message.Function);
            if (logger != null)
            {
                FunctionStarted(logger, message.Function.ShortName, message.FormatReason(), message.FunctionInstanceId, null);

                if (message.TriggerDetails != null && message.TriggerDetails.Count != 0)
                {
                    LogTemplatizedTriggerDetails(logger, message);
                }
            }

            return StringNullTask;
        }

        private void LogTemplatizedTriggerDetails(ILogger logger, FunctionStartedMessage message)
        {
            var triggerDetails = message.TriggerDetails;
            var key = message.Function.FullName;

            if (_triggerLoggers.TryGetValue(key, out var log))
            {
                if (log(logger, triggerDetails))
                {
                    return;
                }

                var newLog = BuildTriggerLogger(triggerDetails);
                newLog(logger, triggerDetails); // must log, it's build on this trigger

                _triggerLoggers.TryUpdate(key, newLog, log); // try replace, may fail if another thread wrote sth
            }
            else
            {
                var newLog = BuildTriggerLogger(triggerDetails);
                newLog(logger, triggerDetails); // must log, it's build on this trigger
                _triggerLoggers.TryAdd(key, newLog);
            }
        }

        private static Func<ILogger, IDictionary<string, string>, bool> BuildTriggerLogger(IDictionary<string, string> triggerDetails)
        {
            var keys = triggerDetails.Keys.ToArray();

            var templateKeys = keys.Select(key => $"{key}: {{{key}}}");
            var messageTemplate = "Trigger Details: " + string.Join(", ", templateKeys);

            string key0;
            string key1;
            string key2;

            switch (keys.Length)
            {
                case 1:
                    var log1 = LoggerMessage.Define<string>(LogLevel.Information, 0, messageTemplate);
                    key0 = keys[0];

                    bool TryLog1(ILogger logger, IDictionary<string, string> values)
                    {
                        if (values.Count != 1)
                        {
                            return false;
                        }

                        if (!values.TryGetValue(key0, out var value0))
                        {
                            return false;
                        }

                        log1(logger, value0, null);
                        return true;
                    }

                    return TryLog1;

                case 2:
                    var log2 = LoggerMessage.Define<string, string>(LogLevel.Information, 0, messageTemplate);
                    key0 = keys[0];
                    key1 = keys[1];

                    bool TryLog2(ILogger logger, IDictionary<string, string> values)
                    {
                        if (values.Count != 2)
                        {
                            return false;
                        }

                        if (!values.TryGetValue(key0, out var value0) ||
                            !values.TryGetValue(key1, out var value1))
                        {
                            return false;
                        }

                        log2(logger, value0, value1, null);
                        return true;
                    }

                    return TryLog2;

                case 3:
                    var log3 = LoggerMessage.Define<string, string, string>(LogLevel.Information, 0, messageTemplate);
                    key0 = keys[0];
                    key1 = keys[1];
                    key2 = keys[2];

                    bool TryLog3(ILogger logger, IDictionary<string, string> values)
                    {
                        if (values.Count != 3)
                        {
                            return false;
                        }

                        if (!values.TryGetValue(key0, out var value0) ||
                            !values.TryGetValue(key1, out var value1) ||
                            !values.TryGetValue(key2, out var value2))
                        {
                            return false;
                        }

                        log3(logger, value0, value1, value2, null);
                        return true;
                    }

                    return TryLog3;

                case 4:
                    var log4 = LoggerMessage.Define<string, string, string, string>(LogLevel.Information, 0, messageTemplate);
                    key0 = keys[0];
                    key1 = keys[1];
                    key2 = keys[2];
                    var key3 = keys[3];

                    bool TryLog4(ILogger logger, IDictionary<string, string> values)
                    {
                        if (values.Count != 3)
                        {
                            return false;
                        }

                        if (!values.TryGetValue(key0, out var value0) ||
                            !values.TryGetValue(key1, out var value1) ||
                            !values.TryGetValue(key2, out var value2) ||
                            !values.TryGetValue(key3, out var value3))
                        {
                            return false;
                        }

                        log4(logger, value0, value1, value2, value3, null);
                        return true;
                    }

                    return TryLog4;

                default:

                    bool Log(ILogger logger, IDictionary<string, string> values)
                    {
                        string[] templateValues = triggerDetails.Values.ToArray();
                        logger.Log(LogLevel.Information, 0, messageTemplate, templateValues);
                        return true;
                    }

                    return Log;
            }
        }

        public Task LogFunctionCompletedAsync(FunctionCompletedMessage message, CancellationToken cancellationToken)
        {
            ILogger logger = GetLogger(message.Function);

            if (logger != null)
            {
                if (message.Succeeded)
                {
                    FunctionCompletedSuccess(logger, message.Function.ShortName, message.FunctionInstanceId, null);
                }
                else
                {
                    FunctionCompletedFailure(logger, message.Function.ShortName, message.FunctionInstanceId, message.Failure.Exception);
                }
            }

            return Task.CompletedTask;
        }

        private ILogger GetLogger(FunctionDescriptor descriptor)
        {
            return _loggerFactory?.CreateLogger(LogCategories.CreateFunctionCategory(descriptor.LogName));
        }

        public Task DeleteLogFunctionStartedAsync(string startedMessageId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
