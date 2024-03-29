﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Indexers
{
    // Test failure cases for indexing
    public class FunctionIndexerIntegrationErrorTests
    {
        [Fact]
        public void TestFails()
        {
            foreach (var method in this.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                Mock<IFunctionExecutor> executorMock = new Mock<IFunctionExecutor>(MockBehavior.Strict);
                Mock<IServiceScopeFactory> scopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
                IFunctionIndexCollector stubIndex = new Mock<IFunctionIndexCollector>().Object;
                IConfiguration configuration = new ConfigurationBuilder().Build();
                var instanceServicesFactory = new DefaultInstanceServicesProviderFactory(scopeFactoryMock.Object);

                FunctionIndexer indexer = new FunctionIndexer(
                    new Mock<ITriggerBindingProvider>(MockBehavior.Strict).Object,
                    new Mock<IBindingProvider>(MockBehavior.Strict).Object,
                    new Mock<IJobActivator>(MockBehavior.Strict).Object,
                    executorMock.Object,
                    new SingletonManager(),
                    null,
                    configuration,
                    instanceServicesFactory);

                Assert.Throws<FunctionIndexingException>(() => indexer.IndexMethodAsync(method, stubIndex, CancellationToken.None).GetAwaiter().GetResult());
            }
        }

        private static void MultipleQueueParams([QueueTrigger("p123")] int p123, [QueueTrigger("p234")] int p234) { }

        private static void QueueNestedIEnumerable([Queue("myoutputqueue")] ICollection<IEnumerable<Payload>> myoutputqueue) { }

        private static void QueueOutputIList([Queue("myoutputqueue")] out IList<Payload> myoutputqueue) { myoutputqueue = null; }

        private static void FuncQueueOutputObject([Queue("myoutputqueue")] out object myoutputqueue) { myoutputqueue = null; }

        private static void FuncQueueOutputIEnumerableOfObject([Queue("myoutputqueue")] out IEnumerable<object> myoutputqueue) { myoutputqueue = null; }

        private class Payload
        {
            public int Value { get; set; }
        }
    }
}