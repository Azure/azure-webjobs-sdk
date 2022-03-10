// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public class NullFunctionInstanceLogger : IFunctionInstanceLogger
    {
        string IFunctionInstanceLogger.LogFunctionStarted(FunctionStartedMessage message)
        {
            return string.Empty;
        }

        void IFunctionInstanceLogger.LogFunctionCompleted(FunctionCompletedMessage message)
        {
        }

        void IFunctionInstanceLogger.DeleteLogFunctionStarted(string startedMessageId)
        {
        }
    }
}
