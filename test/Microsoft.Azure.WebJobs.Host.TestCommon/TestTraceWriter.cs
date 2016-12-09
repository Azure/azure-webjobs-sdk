﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public class TestTraceWriter : TraceWriter
    {
        public Collection<TraceEvent> Traces = new Collection<TraceEvent>();
        private object _syncLock = new object();

        public TestTraceWriter(TraceLevel level) : base(level)
        {
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
