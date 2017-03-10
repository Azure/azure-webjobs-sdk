// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal class LoggerTraceWriter : TraceWriter
    {
        private ILogger _logger;

        public LoggerTraceWriter(TraceLevel level, ILogger logger)
            : base(level)
        {
            _logger = logger;
        }

        public override void Trace(TraceEvent traceEvent)
        {
            if (traceEvent.Level > Level)
            {
                return;
            }

            LogLevel level = GetLogLevel(traceEvent.Level);
            LogValueCollection logState = new LogValueCollection(traceEvent.Message, null, new ReadOnlyDictionary<string, object>(traceEvent.Properties));
            _logger.Log(level, 0, logState, traceEvent.Exception, (s, e) => s.ToString());
        }

        internal static LogLevel GetLogLevel(TraceLevel traceLevel)
        {
            switch (traceLevel)
            {
                case TraceLevel.Off:
                    return LogLevel.None;
                case TraceLevel.Error:
                    return LogLevel.Error;
                case TraceLevel.Warning:
                    return LogLevel.Warning;
                case TraceLevel.Info:
                    return LogLevel.Information;
                case TraceLevel.Verbose:
                    return LogLevel.Debug;
                default:
                    throw new InvalidOperationException($"'{traceLevel}' is not a valid level.");
            }
        }
    }
}
