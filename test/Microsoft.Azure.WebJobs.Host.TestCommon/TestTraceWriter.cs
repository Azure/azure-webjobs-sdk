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
        public Collection<TraceEvent> Traces = new Collection<TraceEvent>();
        private object _syncLock = new object();

        public TestTraceWriter(TraceLevel level) : base(level)
        {
        }

        public IEnumerable<string> GetTraces()
        {
            lock (_syncLock)
            {
                return Traces.Select(t => t.Message).ToList();
            }
        }

        public override void Trace(TraceEvent traceEvent)
        {
            lock (_syncLock)
            {
                Traces.Add(traceEvent);
            }
        }
    }
}
