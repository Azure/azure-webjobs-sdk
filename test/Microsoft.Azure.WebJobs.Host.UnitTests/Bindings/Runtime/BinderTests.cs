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
        var tasks = new List<Task>();

        // Act
        // Start many concurrent tasks that call _binders.Add(). This is not thread-safe
        // and can corrupt the internal state of the List<T>, leading to null entries.
        for (int t = 0; t < 100_000; t++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await binder.BindAsync<object>(new TestAttribute());
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Now that all concurrent binds are done, call Complete.
        // If the list's internal state was corrupted by the concurrent Add calls,
        // this iteration will likely hit a null element.
        await binder.Complete(CancellationToken.None);
    }

    private class TestAttribute : Attribute { }
}
