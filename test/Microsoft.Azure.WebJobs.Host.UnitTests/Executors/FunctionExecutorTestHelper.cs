// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Executors
{
    class FunctionExecutorTestHelper
    {
        public static FunctionDescriptor GetFunctionDescriptor()
        {
            var method = typeof(FunctionExecutorTestHelper).GetMethod(nameof(TestFunction), BindingFlags.NonPublic | BindingFlags.Static);
            return FunctionIndexer.FromMethod(method, new ConfigurationBuilder().Build());
        }

        public static IFunctionInstance CreateFunctionInstance(Guid id, IDictionary<string, string> triggerDetails, bool invocationThrows, FunctionDescriptor descriptor, int delayInMilliseconds = 0)
        {
            var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
            var serviceScopeMock = new Mock<IServiceScope>();
            serviceScopeFactoryMock.Setup(s => s.CreateScope()).Returns(serviceScopeMock.Object);
            Mock<IFunctionInvoker> mockInvoker = new Mock<IFunctionInvoker>();
            int invocationCount = 0;
            mockInvoker.Setup(i => i.InvokeAsync(It.IsAny<object>(), It.IsAny<object[]>()))
                .Returns(async () =>
                {
                    invocationCount++;
                    await Task.Delay(delayInMilliseconds);
                    if (invocationThrows)
                    {
                        throw new Exception($"Test retry exception. invocationCount:{invocationCount}");
                    }
                    return Task.FromResult<object>(null);
                });
            mockInvoker.Setup(m => m.ParameterNames).Returns(new List<string>());
            var mockBindingSource = new Mock<IBindingSource>();
            var valueProviders = Task.Run(() =>
            {
                IDictionary<string, IValueProvider> d = new Dictionary<string, IValueProvider>();
                IReadOnlyDictionary<string, IValueProvider> red = new ReadOnlyDictionary<string, IValueProvider>(d);
                return red;
            });
            mockBindingSource.Setup(p => p.BindAsync(It.IsAny<ValueBindingContext>())).Returns(valueProviders);
            return new FunctionInstance(id, triggerDetails, null, new ExecutionReason(), mockBindingSource.Object, mockInvoker.Object, descriptor, serviceScopeFactoryMock.Object);
        }

        [FixedDelayRetry(5, "00:00:01")]
        private static void TestFunction()
        {
            // used for a FunctionDescriptor
        }
    }
}
