// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Timers;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    /// <summary>
    /// Logs the diagnostics events emitted by the ApplicationInsights SDK.
    /// Logs data either every 10 seconds or when the batch becomes full, whichever occurs first.
    /// Logs in batch to reduce the volume of logs in kusto
    /// </summary>
    internal class ApplicationInsightsEventListener : EventListener
    {

        private static readonly DiagnosticListener _source = new DiagnosticListener("Microsoft.Azure.Functions.Host.ApplicationInsightsEventListener");
        
        private readonly EventLevel _eventLevel;

        private const int LogFlushIntervalMs = 10 * 1000;
        private const string EventSourceNamePrefix = "Microsoft-ApplicationInsights-";
        private const string EventName = "ApplicationInsightsEventListener";
        private const int MaxLogLinesPerFlushInterval = 30;

        private Timer _flushTimer;
        private ConcurrentQueue<string> _logBuffer = new ConcurrentQueue<string>();
        private ConcurrentQueue<EventSource> _eventSource = new ConcurrentQueue<EventSource>();
        private static object _syncLock = new object();        
        private bool _disposed = false;        

        public ApplicationInsightsEventListener(EventLevel eventLevel)
        {
            this._eventLevel = eventLevel;
            _flushTimer = new Timer
            {
                AutoReset = true,
                Interval = LogFlushIntervalMs
            };
            _flushTimer.Elapsed += (sender, e) => Flush();
            _flushTimer.Start();
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {            
            if (eventSource.Name.StartsWith(EventSourceNamePrefix))
            {
                EnableEvents(eventSource, _eventLevel, EventKeywords.All);
                _eventSource.Enqueue(eventSource);
            }
            base.OnEventSourceCreated(eventSource);
        }
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (!string.IsNullOrWhiteSpace(eventData.Message))
            {
                 _logBuffer.Enqueue(string.Format(eventData.Message, eventData.Payload.ToArray()));   
                if (_logBuffer.Count >= MaxLogLinesPerFlushInterval)
                {
                    Flush();
                }
            }
        }

        public void Flush()
        {
            if (_logBuffer.Count == 0)
            {
                return;
            }
            lock (_syncLock)
            {
                // batch up to 30 events in one log
                StringBuilder sb = new StringBuilder();
                // start with a new line
                sb.AppendLine(string.Empty);
                string line = null;
                while (_logBuffer.TryDequeue(out line))
                {                    
                    sb.AppendLine(line);
                }
                _source.Write(EventName, sb.ToString());
            }            
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    EventSource source;
                    while (_eventSource.TryDequeue(out source))
                    {
                       if (source != null)
                        {
                            source.Dispose();
                        }
                    }

                    if (_flushTimer != null)
                    {
                        _flushTimer.Dispose();
                    }
                    // ensure any remaining logs are flushed
                    Flush();
                }

                _disposed = true;
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
