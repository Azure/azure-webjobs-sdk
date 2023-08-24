// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Bindings
{
    public class AmbientBindingContextTests
    {
        [Fact]
        public void Constructor_BindingData_InitializesMembers()
        {
            var functionCancellationToken = new CancellationToken();
            var functionBindingContext = new FunctionBindingContext(Guid.NewGuid(), functionCancellationToken);
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            var context = new AmbientBindingContext(functionBindingContext, bindingData);

            Assert.Same(functionBindingContext, context.FunctionContext);
            Assert.Same(bindingData, context.BindingData);
            Assert.Equal(functionCancellationToken, context.FunctionCancellationToken);
            Assert.Equal(functionBindingContext.FunctionInstanceId, context.FunctionInstanceId);
        }

        [Fact]
        public void Constructor_BindingDataFactory_InitializesMembers()
        {
            var functionCancellationToken = new CancellationToken();
            var functionBindingContext = new FunctionBindingContext(Guid.NewGuid(), functionCancellationToken);
            int invokeCount = 0;
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            Func<IReadOnlyDictionary<string, object>> factory = () =>
            {
                invokeCount++;
                return bindingData;
            };
            var context = new AmbientBindingContext(functionBindingContext, factory);

            Assert.Equal(0, invokeCount);
            Assert.Equal(functionCancellationToken, context.FunctionCancellationToken);
            Assert.Equal(functionBindingContext.FunctionInstanceId, context.FunctionInstanceId);

            Assert.Same(bindingData, context.BindingData);
            Assert.Equal(1, invokeCount);

            // factory only called once
            Assert.Same(bindingData, context.BindingData);
            Assert.Same(bindingData, context.BindingData);
            Assert.Equal(1, invokeCount);
        }
    }
}
