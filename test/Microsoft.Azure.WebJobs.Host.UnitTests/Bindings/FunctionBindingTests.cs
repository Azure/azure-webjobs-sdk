// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Bindings
{
    public class FunctionBindingTests
    {
        [Fact]
        public void NewBindingContext_LazyBindingData()
        {
            var cancellationToken = new CancellationToken();
            var functionCancellationToken = new CancellationToken();
            var functionDescriptor = new FunctionDescriptor
            {
                LogName = "Test"
            };
            var functionBindingContext = new FunctionBindingContext(Guid.NewGuid(), functionCancellationToken, functionDescriptor);
            var valueContext = new ValueBindingContext(functionBindingContext, cancellationToken);
            Dictionary<string, object> bindingData = new Dictionary<string, object>
            {
                { "d1", 1 },
                { "d2", 2 },
                { "d3", 3 }
            };
            Dictionary<string, object> parameters = new Dictionary<string, object>()
            {
                { "p1", 1 },
                { "p2", 2 },
                { "p3", 3 },
                { "d3", "Overridden" }
            };

            var context = FunctionBinding.NewBindingContext(valueContext, bindingData, parameters);

            Assert.Same(valueContext, context.ValueContext);
            Assert.Equal(cancellationToken, context.CancellationToken);
            Assert.Equal(functionCancellationToken, context.FunctionCancellationToken);
            Assert.Equal(functionBindingContext.FunctionInstanceId, context.FunctionInstanceId);

            var bindingContextField = typeof(BindingContext).GetField("_bindingData", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.Null(bindingContextField.GetValue(context));
            var result = context.BindingData;
            Assert.NotSame(bindingData, context.BindingData);
            Assert.NotNull(bindingContextField.GetValue(context));

            // make sure factory only called once
            Assert.Same(result, context.BindingData);
            Assert.Same(result, context.BindingData);
            Assert.Same(result, context.BindingData);

            Assert.Equal(7, context.BindingData.Count);
            Assert.Equal("Overridden", context.BindingData["d3"]);
            var sys = (SystemBindingData)context.BindingData["sys"];
            Assert.Equal("Test", sys.MethodName);
        }
    }
}
