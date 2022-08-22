// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal class CompositeFunctionInstanceLogger : IFunctionInstanceLogger
    {
        private readonly IFunctionInstanceLogger[] _loggers;

        public CompositeFunctionInstanceLogger(params IFunctionInstanceLogger[] loggers)
        {
            _loggers = loggers;
        }

        public string LogFunctionStarted(FunctionStartedMessage message)
        {
            string startedMessageId = null;

            foreach (IFunctionInstanceLogger logger in _loggers)
            {
                var messageId = logger.LogFunctionStarted(message);
                if (!String.IsNullOrEmpty(messageId))
                {
                    if (String.IsNullOrEmpty(startedMessageId))
                    {
                        startedMessageId = messageId;
                    }
                    else if (startedMessageId != messageId)
                    {
                        throw new NotSupportedException();
                    }
                }
            }

            return startedMessageId;
        }

        public void LogFunctionCompleted(FunctionCompletedMessage message)
        {
            foreach (IFunctionInstanceLogger logger in _loggers)
            {
                logger.LogFunctionCompleted(message);
            }
        }

        public void DeleteLogFunctionStarted(string startedMessageId)
        {
            foreach (IFunctionInstanceLogger logger in _loggers)
            {
                logger.DeleteLogFunctionStarted(startedMessageId);
            }
        }
    }
}
