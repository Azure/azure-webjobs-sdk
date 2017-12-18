// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Microsoft.Azure.WebJobs.Logging
{
    /// <summary>
    /// A <see cref="TraceWriter"/> wrapper around an <see cref="ILogger"/>.  
    /// </summary>
    internal class LoggerTraceWriter : TraceWriter
    {
        private ILogger _logger;

        /// <summary>
        /// Creates an instance.
        /// </summary>
        /// <param name="level">The <see cref="TraceLevel"/> to use when filtering logs.</param>        
        public LoggerTraceWriter(ILogger logger)
            : base(TraceLevel.Verbose)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public override void Trace(TraceEvent traceEvent)
        {
            if (traceEvent == null)
            {
                throw new ArgumentNullException(nameof(traceEvent));
            }

            LogLevel level = GetLogLevel(traceEvent.Level);
            FormattedLogValuesCollection logState = new FormattedLogValuesCollection(traceEvent.Message, null, new ReadOnlyDictionary<string, object>(traceEvent.Properties));
            _logger?.Log(level, 0, logState, traceEvent.Exception, (s, e) => s.ToString());
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
