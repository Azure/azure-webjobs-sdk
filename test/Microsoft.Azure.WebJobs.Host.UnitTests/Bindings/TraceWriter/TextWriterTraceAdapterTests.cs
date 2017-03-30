﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Bindings
{
    public class TextWriterTraceAdapterTests
    {
        private readonly Mock<TraceWriter> _mockTraceWriter;
        private readonly TextWriter _adapter;

        public TextWriterTraceAdapterTests()
        {
            _mockTraceWriter = new Mock<TraceWriter>(MockBehavior.Strict, TraceLevel.Verbose);
            _adapter = TextWriterTraceAdapter.Synchronized(_mockTraceWriter.Object);
        }

        [Fact]
        public void Write_SingleCharacterWrites_BuffersUntilNewline()
        {
            _mockTraceWriter.Setup(p => p.Trace(It.Is<TraceEvent>(q => q.Level == TraceLevel.Info && q.Message == "Mathew\r\n")));

            _adapter.Write('M');
            _adapter.Write('a');
            _adapter.Write('t');
            _adapter.Write('h');
            _adapter.Write('e');
            _adapter.Write('w');
            _adapter.WriteLine();

            _mockTraceWriter.VerifyAll();
        }

        [Fact]
        public void Write_VariousWriteOverloads_BuffersUntilNewline()
        {
            _mockTraceWriter.Setup(p => p.Trace(It.Is<TraceEvent>(q => q.Level == TraceLevel.Info && q.Message == "=====================\r\n")));
            _mockTraceWriter.Setup(p => p.Trace(It.Is<TraceEvent>(q => q.Level == TraceLevel.Info && q.Message == "TestData123456True=====================\r\n")));
            _mockTraceWriter.Setup(p => p.Trace(It.Is<TraceEvent>(q => q.Level == TraceLevel.Info && q.Message == "This is a new line\r\n")));
            _mockTraceWriter.Setup(p => p.Trace(It.Is<TraceEvent>(q => q.Level == TraceLevel.Info && q.Message == "This is some more text")));
            _mockTraceWriter.Setup(p => p.Flush());

            _adapter.Write("=====================\r\n");
            _adapter.Write("TestData");
            _adapter.Write(123456);
            _adapter.Write(true);
            _adapter.Write("=====================\r\n");
            _adapter.WriteLine("This is a new line");
            _adapter.Write("This is some more text");

            _adapter.Flush();
        }

        [Fact]
        public void Flush_FlushesRemainingBuffer()
        {
            _mockTraceWriter.Setup(p => p.Trace(It.Is<TraceEvent>(q => q.Level == TraceLevel.Info && q.Message == "This is a test")));
            _mockTraceWriter.Setup(p => p.Flush());

            _adapter.Write("This");
            _adapter.Write(" is ");
            _adapter.Write("a ");
            _adapter.Write("test");
            _adapter.Flush();

            _mockTraceWriter.VerifyAll();
        }

        [Fact]
        public void Flush_ClearsInternalBuffer()
        {
            _mockTraceWriter.Setup(p => p.Trace(It.IsAny<TraceEvent>()));
            _mockTraceWriter.Setup(p => p.Flush());

            _adapter.Write("This");
            _adapter.Write(" is ");
            _adapter.Write("a ");
            _adapter.Write("test");
            _adapter.Flush();
            _adapter.Flush();

            _mockTraceWriter.Verify(p => p.Trace(It.Is<TraceEvent>(q => q.Level == TraceLevel.Info && q.Message == "This is a test")), Times.Exactly(1));
        }

        [Fact]
        public async Task TestMultipleThreads()
        {
            // This validates a bug where writing from multiple threads throws an exception.             
            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Verbose);
            TextWriter adapter = TextWriterTraceAdapter.Synchronized(trace);

            // Start Tasks to write
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 1000; i++)
            {
                tasks.Add(adapter.WriteLineAsync(string.Empty));
            }

            await Task.WhenAll(tasks);

            Assert.Equal(1000, trace.Traces.Count);
        }
    }
}
