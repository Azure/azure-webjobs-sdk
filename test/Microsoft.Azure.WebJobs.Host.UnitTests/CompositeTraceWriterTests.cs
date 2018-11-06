﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class CompositeTraceWriterTests
    {
        private readonly Mock<TraceWriter> _mockTraceWriter;
        private readonly Mock<TextWriter> _mockTextWriter;
        private readonly CompositeTraceWriter _traceWriter;

        public CompositeTraceWriterTests()
        {
            _mockTextWriter = new Mock<TextWriter>(MockBehavior.Strict);
            _mockTextWriter.Setup(m => m.FormatProvider)
                .Returns(new Mock<IFormatProvider>(MockBehavior.Strict).Object);

            _mockTraceWriter = new Mock<TraceWriter>(MockBehavior.Strict, TraceLevel.Warning);
            _traceWriter = new CompositeTraceWriter(_mockTraceWriter.Object, _mockTextWriter.Object);
        }

        [Fact]
        public void Trace_FiltersOnOwnLevel()
        {
            _traceWriter.Level = TraceLevel.Warning;

            _mockTraceWriter.Setup(p => p.Trace(It.Is<TraceEvent>(q => q.Level == TraceLevel.Warning && q.Source == "TestSource" && q.Message == "Test Warning" && q.Exception == null)));
            _mockTextWriter.Setup(p => p.WriteLine("Test Warning"));

            _traceWriter.Info("Test Information", source: "TestSource"); // don't expect this to be logged
            _traceWriter.Warning("Test Warning", source: "TestSource");

            _mockTextWriter.VerifyAll();
            _mockTraceWriter.VerifyAll();
        }

        [Fact]
        public void Trace_DelegatesToInnerTraceWriterAndTextWriter()
        {
            _mockTraceWriter.Setup(p => p.Trace(It.Is<TraceEvent>(q => q.Level == TraceLevel.Warning && q.Source == "TestSource" && q.Message == "Test Warning" && q.Exception == null)));
            _mockTraceWriter.Setup(p => p.Trace(It.Is<TraceEvent>(q => q.Level == TraceLevel.Error && q.Source == "TestSource" && q.Message == "Test Error" && q.Exception == null)));
            Exception ex = new Exception("Kaboom!");
            _mockTraceWriter.Setup(p => p.Trace(It.Is<TraceEvent>(q => q.Level == TraceLevel.Error && q.Source == "TestSource" && q.Message == "Test Error With Exception" && q.Exception == ex)));

            _mockTextWriter.Setup(p => p.WriteLine("Test Information"));
            _mockTextWriter.Setup(p => p.WriteLine("Test Warning"));
            _mockTextWriter.Setup(p => p.WriteLine("Test Error"));
            _mockTextWriter.Setup(p => p.WriteLine("Test Error With Exception"));
            _mockTextWriter.Setup(p => p.WriteLine(ex.ToDetails()));

            _traceWriter.Info("Test Information", source: "TestSource");  // don't expect this to be logged
            _traceWriter.Warning("Test Warning", source: "TestSource");
            _traceWriter.Error("Test Error", source: "TestSource");
            _traceWriter.Error("Test Error With Exception", ex, source: "TestSource");

            _mockTextWriter.VerifyAll();
            _mockTraceWriter.VerifyAll();
        }

        [Fact]
        public void Trace_CustomProperties()
        {
            TraceEvent capturedEvent = null;
            _mockTraceWriter.Setup(p => p.Trace(It.IsAny<TraceEvent>()))
                .Callback<TraceEvent>(p =>
                {
                    capturedEvent = p;
                });
            _mockTextWriter.Setup(p => p.WriteLine("Test Warning"));

            TraceEvent traceEvent = new TraceEvent(TraceLevel.Warning, "Test Warning", "Test Source");
            traceEvent.Properties.Add("Test", "Test Property");

            _traceWriter.Trace(traceEvent);

            _mockTraceWriter.VerifyAll();
            _mockTextWriter.VerifyAll();

            Assert.Same(traceEvent, capturedEvent);
            Assert.Equal("Test Property", capturedEvent.Properties["Test"]);
        }

        [Fact]
        public void Flush_FlushesInnerTraceWriterAndTextWriter()
        {
            _mockTraceWriter.Setup(p => p.Flush());
            _mockTextWriter.Setup(p => p.Flush());

            _traceWriter.Flush();

            _mockTextWriter.VerifyAll();
            _mockTraceWriter.VerifyAll();
        }

        [Fact]
        public void Constructor_CreatesCopyOfCollection()
        {
            var textWriter = new StringWriter();
            var t1 = new TestTraceWriter(TraceLevel.Verbose);
            var t2 = new TestTraceWriter(TraceLevel.Verbose);
            List<TraceWriter> traceWriters = new List<TraceWriter> { t1, t2 };
            var traceWriter = new CompositeTraceWriter(traceWriters, textWriter);

            traceWriter.Info("Test");
            Assert.Equal(1, t1.GetTraces().Count);
            Assert.Equal(1, t2.GetTraces().Count);
            Assert.Equal(1, textWriter.ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Length);

            var t3 = new TestTraceWriter(TraceLevel.Verbose);
            traceWriters.Add(t3);

            traceWriter.Info("Test");
            Assert.Equal(2, t1.GetTraces().Count);
            Assert.Equal(2, t2.GetTraces().Count);
            Assert.Equal(2, textWriter.ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.Equal(0, t3.GetTraces().Count);
        }

        [Fact]
        public void TextWriter_IsSynchronized()
        {
            var textWriter = new StreamWriter(new MemoryStream());
            var traceWriter = new CompositeTraceWriter(Enumerable.Empty<TraceWriter>(), textWriter);

            ExceptionDispatchInfo exception = null;

            List<Thread> threads = new List<Thread>();
            for (int i = 0; i < 5; i++)
            {
                var t = new Thread(() =>
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        try
                        {
                            traceWriter.Info($"{i};{j}");
                        }
                        catch (Exception ex)
                        {
                            exception = ExceptionDispatchInfo.Capture(ex);
                            throw;
                        }
                    }
                });

                threads.Add(t);
            }

            foreach (var t in threads)
            {
                t.Start();
            }

            foreach (var t in threads)
            {
                t.Join();
            }

            if (exception != null)
            {
                exception.Throw();
            }
        }
    }
}
