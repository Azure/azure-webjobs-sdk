// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Executors
{
    public class CompositeFunctionEventCollectorTests
    {
        [Fact]
        public void Dispose_Disposes()
        {
            // Test a couple collectors that implement IDisposable, and a couple that don't.
            var collection = new Collection<IAsyncCollector<FunctionInstanceLogEntry>>();

            var mockDisposable1 = new Mock<IDisposable>(MockBehavior.Strict);
            mockDisposable1.Setup(p => p.Dispose());
            collection.Add(mockDisposable1.As<IAsyncCollector<FunctionInstanceLogEntry>>().Object);

            var mockDisposable2 = new Mock<IDisposable>(MockBehavior.Strict);
            mockDisposable2.Setup(p => p.Dispose());
            collection.Add(mockDisposable2.As<IAsyncCollector<FunctionInstanceLogEntry>>().Object);

            collection.Add(new Mock<IAsyncCollector<FunctionInstanceLogEntry>>(MockBehavior.Strict).Object);
            collection.Add(new Mock<IAsyncCollector<FunctionInstanceLogEntry>>(MockBehavior.Strict).Object);

            var collector = new CompositeFunctionEventCollector(collection.ToArray());
            collector.Dispose();

            mockDisposable1.Verify(p => p.Dispose(), Times.Once);
            mockDisposable2.Verify(p => p.Dispose(), Times.Once);
        }
    }
}
