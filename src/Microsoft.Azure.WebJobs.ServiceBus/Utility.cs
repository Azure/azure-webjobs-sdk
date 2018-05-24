// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    internal static class Utility
    {
        public static void LogExceptionReceivedEvent(ExceptionReceivedEventArgs e, string source, TraceWriter traceWriter, ILoggerFactory loggerFactory = null)
        {
            var logger = loggerFactory?.CreateLogger(LogCategories.Executor);
            string message = $"{source} error (Action={e.Action})";

            var mex = e.Exception as MessagingException;
            if (mex == null || !mex.IsTransient)
            {
                // any non-transient exceptions or unknown exception types
                // we want to log as errors
                logger?.LogError(0, e.Exception, message);
                traceWriter.Error(message, e.Exception);
            }
            else
            {
                // transient messaging errors we log as verbose so we have a record
                // of them, but we don't treat them as actual errors
                logger?.LogDebug(0, e.Exception, message);
                var evt = new TraceEvent(TraceLevel.Verbose, $"{message} : {e.Exception.ToString()}", exception: e.Exception);
                traceWriter.Trace(evt);
            }
        }
    }
}
