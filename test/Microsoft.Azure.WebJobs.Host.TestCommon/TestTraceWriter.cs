// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public class TestTraceWriter : TraceWriter
    {
        private Collection<TraceEvent> _traces = new Collection<TraceEvent>();
        private object _syncObject = new object();

        public TestTraceWriter(TraceLevel level) : base(level)
        {
        }

        public bool Flushed { get; private set; }

        // Don't allow direct access to the underlying _traces as they can be modified
        // while callers are enumerating, resulted in a 'Collection was modified' exception.
        public IList<TraceEvent> GetTraces()
        {
            lock (_syncObject)
            {
                return _traces.ToList();
            }
        }

        public void ClearTraces()
        {
            lock (_syncObject)
            {
                _traces.Clear();
            }
        }

        public override void Trace(TraceEvent traceEvent)
        {
            lock (_syncObject)
            {
                _traces.Add(traceEvent);
            }
        }

        public override void Flush()
        {
            Flushed = true;

            base.Flush();
        }
    }
}
