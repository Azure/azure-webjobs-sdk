﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests.TestDoubles
{
    internal class ExpectInstanceFailureTaskFunctionInstanceLogger : IFunctionInstanceLogger
    {
        private readonly TaskCompletionSource<Exception> _taskSource;

        public ExpectInstanceFailureTaskFunctionInstanceLogger(TaskCompletionSource<Exception> taskSource)
        {
            _taskSource = taskSource;
        }

        public string LogFunctionStarted(FunctionStartedMessage message)
        {
            return string.Empty;
        }

        public void LogFunctionCompleted(FunctionCompletedMessage message)
        {
            if (message != null)
            {
                // This class is used when a function is expected to fail (the result of the task is the expected
                // exception).
                // A faulted task is reserved for unexpected failures (like unhandled background exceptions).
                _taskSource.SetResult(message.Failure != null ? message.Failure.Exception : null);
            }
        }

        public void DeleteLogFunctionStarted(string startedMessageId)
        {
        }
    }
}
