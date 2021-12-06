// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    // This is a DI interface.
    internal interface IFunctionInstanceLogger
    {
        string LogFunctionStarted(FunctionStartedMessage message);

        void LogFunctionCompleted(FunctionCompletedMessage message);

        void DeleteLogFunctionStarted(string startedMessageId);
    }
}
