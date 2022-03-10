// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests.TestDoubles
{
    internal class ExpectInstanceSuccessTaskFunctionInstanceLogger : IFunctionInstanceLogger
    {
        private readonly TaskCompletionSource<object> _taskSource;

        public ExpectInstanceSuccessTaskFunctionInstanceLogger(TaskCompletionSource<object> taskSource)
        {
            _taskSource = taskSource;
        }

        public string LogFunctionStarted(FunctionStartedMessage message)
        {
            return string.Empty;
        }

        public void LogFunctionCompleted(FunctionCompletedMessage message)
        {
            if (message != null && message.Failure != null)
            {
                _taskSource.SetException(message.Failure.Exception);
            }
            else
            {
                _taskSource.SetResult(null);
            }
        }

        public void DeleteLogFunctionStarted(string startedMessageId)
        {
        }
    }
}
