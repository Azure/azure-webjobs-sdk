// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Bindings
{
    public class BindingContextTests
    {
        [Fact]
        public void Constructor_BindingData_InitializesMembers()
        {
            var cancellationToken = new CancellationToken();
            var functionCancellationToken = new CancellationToken();
            var functionBindingContext = new FunctionBindingContext(Guid.NewGuid(), functionCancellationToken);
            var valueContext = new ValueBindingContext(functionBindingContext, cancellationToken);
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            var context = new BindingContext(valueContext, bindingData);

            Assert.Same(valueContext, context.ValueContext);
            Assert.Same(bindingData, context.BindingData);
            Assert.Equal(cancellationToken, context.CancellationToken);
            Assert.Equal(functionCancellationToken, context.FunctionCancellationToken);
            Assert.Equal(functionBindingContext.FunctionInstanceId, context.FunctionInstanceId);
        }

        [Fact]
        public void Constructor_BindingDataFactory_InitializesMembers()
        {
            var cancellationToken = new CancellationToken();
            var functionCancellationToken = new CancellationToken();
            var functionBindingContext = new FunctionBindingContext(Guid.NewGuid(), functionCancellationToken);
            var valueContext = new ValueBindingContext(functionBindingContext, cancellationToken);
            int invokeCount = 0;
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            Func<IReadOnlyDictionary<string, object>> factory = () =>
            {
                invokeCount++;
                return bindingData;
            };
            var context = new BindingContext(valueContext, factory);

            Assert.Equal(0, invokeCount);
            Assert.Same(valueContext, context.ValueContext);
            Assert.Equal(cancellationToken, context.CancellationToken);
            Assert.Equal(functionCancellationToken, context.FunctionCancellationToken);
            Assert.Equal(functionBindingContext.FunctionInstanceId, context.FunctionInstanceId);

            var ambientContext = context.AmbientContext;
            Assert.Equal(0, invokeCount);

            Assert.Same(bindingData, context.BindingData);
            Assert.Equal(1, invokeCount);

            Assert.Same(context.BindingData, ambientContext.BindingData);
            Assert.Equal(1, invokeCount);

            // factory only called once
            Assert.Same(bindingData, context.BindingData);
            Assert.Same(bindingData, context.BindingData);
            Assert.Equal(1, invokeCount);
        }
    }
}
