// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    internal abstract class MessagingExceptionHandler
    {
        private readonly TraceWriter _traceWriter;
        private readonly ILoggerFactory _loggerFactory;
        private string _source;

        public MessagingExceptionHandler(string source, TraceWriter traceWriter, ILoggerFactory loggerFactory = null)
        {
            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (traceWriter == null)
            {
                throw new ArgumentNullException(nameof(traceWriter));
            }

            _source = source;
            _traceWriter = traceWriter;
            _loggerFactory = loggerFactory;
        }

        public static MessagingExceptionHandler Subscribe(EventProcessorOptions options, TraceWriter traceWriter, ILoggerFactory loggerFactory = null)
        {
            var exceptionHandler = new EventHubExceptionHandler(options, traceWriter, loggerFactory);
            exceptionHandler.Subscribe();
            return exceptionHandler;
        }

        public static MessagingExceptionHandler Subscribe(OnMessageOptions options, TraceWriter traceWriter, ILoggerFactory loggerFactory = null)
        {
            var exceptionHandler = new ServiceBusExceptionHandler(options, traceWriter, loggerFactory);
            exceptionHandler.Subscribe();
            return exceptionHandler;
        }

        public abstract void Subscribe();

        public abstract void Unsubscribe();

        protected void Handle(object sender, ExceptionReceivedEventArgs e)
        {
            LogExceptionReceivedEvent(e);
        }

        internal void LogExceptionReceivedEvent(ExceptionReceivedEventArgs e)
        {
            try
            {
                var logger = _loggerFactory?.CreateLogger(LogCategories.Executor);
                string message = $"{_source} error (Action={e.Action}) : {e.Exception.ToString()}";

                var logLevel = GetLogLevel(e.Exception);
                logger?.Log(logLevel, 0, message, e.Exception, (s, ex) => message);

                var traceEvent = new TraceEvent(logLevel.ToTraceLevel(), message, null, e.Exception);
                _traceWriter.Trace(traceEvent);
            }
            catch
            {
                // best effort logging
            }
        }

        protected virtual LogLevel GetLogLevel(Exception ex)
        {
            var mex = ex as MessagingException;
            if (!(ex is OperationCanceledException) && (mex == null || !mex.IsTransient))
            {
                // any non-transient exceptions or unknown exception types
                // we want to log as errors
                return LogLevel.Error;
            }
            else
            {
                // transient messaging errors we log as verbose so we have a record
                // of them, but we don't treat them as actual errors
                return LogLevel.Information;
            }
        }

        private class EventHubExceptionHandler : MessagingExceptionHandler
        {
            private readonly EventProcessorOptions _options;

            public EventHubExceptionHandler(EventProcessorOptions options, TraceWriter traceWriter, ILoggerFactory loggerFactory = null) 
                : base("EventProcessorHost", traceWriter, loggerFactory)
            {
                if (options == null)
                {
                    throw new ArgumentNullException(nameof(options));
                }

                _options = options;
            }

            public override void Subscribe()
            {
                _options.ExceptionReceived += Handle;
            }

            public override void Unsubscribe()
            {
                _options.ExceptionReceived -= Handle;
            }

            protected override LogLevel GetLogLevel(Exception ex)
            {
                if (ex is ReceiverDisconnectedException ||
                    ex is LeaseLostException)
                {
                    // For EventProcessorHost these exceptions can happen as part
                    // of normal partition balancing across instances, so we want to
                    // trace them, but not treat them as errors.
                    return LogLevel.Information;
                }

                return base.GetLogLevel(ex);
            }
        }

        private class ServiceBusExceptionHandler : MessagingExceptionHandler
        {
            private readonly OnMessageOptions _options;

            public ServiceBusExceptionHandler(OnMessageOptions options, TraceWriter traceWriter, ILoggerFactory loggerFactory = null) 
                : base("MessageReceiver", traceWriter, loggerFactory)
            {
                if (options == null)
                {
                    throw new ArgumentNullException(nameof(options));
                }

                _options = options;
            }

            public override void Subscribe()
            {
                _options.ExceptionReceived += Handle;
            }

            public override void Unsubscribe()
            {
                _options.ExceptionReceived -= Handle;
            }
        }
    }
}
