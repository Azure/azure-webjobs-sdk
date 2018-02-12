﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// This <see cref="TraceWriter"/> delegates to an inner <see cref="TraceWriter"/> and <see cref="TextWriter"/>.
    /// </summary>
    internal class CompositeTraceWriter : TraceWriter
    {
        private readonly ReadOnlyCollection<TraceWriter> _innerTraceWriters;
        private readonly TextWriter _innerTextWriter;

        public CompositeTraceWriter(IEnumerable<TraceWriter> traceWriters, TextWriter textWriter, TraceLevel traceLevel = TraceLevel.Verbose)
            : base(traceLevel)
        {
            // create a copy of the collection to ensure it isn't modified
            var traceWritersCopy = traceWriters?.ToList() ?? new List<TraceWriter>();
            _innerTraceWriters = traceWritersCopy.AsReadOnly();

            _innerTextWriter = textWriter;
        }

        public CompositeTraceWriter(TraceWriter traceWriter, TextWriter textWriter, TraceLevel traceLevel = TraceLevel.Verbose)
            : this(traceWriter != null ? new List<TraceWriter> { traceWriter } : null, textWriter, traceLevel)
        {
        }

        public override void Trace(TraceEvent traceEvent)
        {
            if (traceEvent == null)
            {
                throw new ArgumentNullException("traceEvent");
            }

            // Apply our top level trace filter first
            if (Level >= traceEvent.Level)
            {
                InvokeTraceWriters(traceEvent);
                InvokeTextWriter(traceEvent);
            }
        }

        protected virtual void InvokeTraceWriters(TraceEvent traceEvent)
        {
            foreach (TraceWriter traceWriter in _innerTraceWriters)
            {
                // filter based on level before delegating
                if (traceWriter.Level >= traceEvent.Level)
                {
                    traceWriter.Trace(traceEvent);
                }
            }
        }

        protected virtual void InvokeTextWriter(TraceEvent traceEvent)
        {
            if (_innerTextWriter != null)
            {
                string message = traceEvent.Message;
                if (!string.IsNullOrEmpty(message) &&
                     message.EndsWith("\r\n", StringComparison.OrdinalIgnoreCase))
                {
                    // remove any terminating return+line feed, since we're
                    // calling WriteLine below
                    message = message.Substring(0, message.Length - 2);
                }

                _innerTextWriter.WriteLine(message);
                if (traceEvent.Exception != null)
                {
                    _innerTextWriter.WriteLine(traceEvent.Exception.ToDetails());
                }
            }
        }

        public override void Flush()
        {
            foreach (TraceWriter traceWriter in _innerTraceWriters)
            {
                traceWriter.Flush();
            }

            if (_innerTextWriter != null)
            {
                _innerTextWriter.Flush();
            }
        }
    }
}
