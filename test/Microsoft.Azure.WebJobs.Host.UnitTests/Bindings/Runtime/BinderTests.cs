// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Runtime;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Bindings.Runtime;

public class BinderTests
{
    [Fact]
    public async Task Complete_AfterConcurrentBinds_ThrowsNullReferenceException()
    {
        // Arrange
        var mockValueBinder = new Mock<IValueBinder>();
        mockValueBinder.Setup(m => m.Type).Returns(typeof(object));

        var mockBinding = new Mock<IBinding>();
        mockBinding.Setup(b => b.BindAsync(It.IsAny<BindingContext>())).ReturnsAsync(mockValueBinder.Object);
        mockBinding.Setup(b => b.ToParameterDescriptor()).Returns(new ParameterDescriptor());

        var mockBindingSource = new Mock<IAttributeBindingSource>();
        mockBindingSource.Setup(s => s.BindAsync<object>(It.IsAny<Attribute>(), It.IsAny<Attribute[]>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(mockBinding.Object);

        var functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None, null);
        mockBindingSource.Setup(s => s.AmbientBindingContext).Returns(new AmbientBindingContext(functionContext, new Dictionary<string, object>().AsReadOnly()));

        var binder = new Binder(mockBindingSource.Object);
        var threads = new List<Thread>();

        const int numConcurrentThreads = 100;
        const int numTasksPerThread = 100;

        // Call BindAsync simultaneously from many threads. There was a race where this would corrupt
        // the internal _binders list.
        for (int i = 0; i < numConcurrentThreads; i++)
        {
            threads.Add(new Thread(() =>
            {
                var tasks = new List<Task>();
                for (int t = 0; t < numTasksPerThread; t++)
                {
                    tasks.Add(binder.BindAsync<object>(new TestAttribute()));
                }

                Task.WaitAll(tasks.ToArray());
            }));
        }

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        // Now that all concurrent binds are done, call Complete.
        // If the list's internal state was corrupted by the concurrent Add calls,
        // this iteration will likely hit a null element.
        await binder.Complete(CancellationToken.None);

        // Because List resizing is not deterministic, another side effect is having a
        // different count of binders than expected.
        var binders = typeof(Binder)
            .GetField("_binders", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(binder) as IList<IValueBinder>;

        Assert.Equal(numConcurrentThreads * numTasksPerThread, binders.Count);
    }

    private class TestAttribute : Attribute { }
}
