// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Executors
{
    public class DefaultHostIdProviderTests
    {
        [Fact]
        public async Task GetHostIdAsync_ReturnsExpectedResult()
        {
            // ensure that the below job method is discovered and this test
            // assembly is used to generate the ID
            var type = GetType();
            var firstJobMethod = FunctionIndexer.GetJobMethods(type).FirstOrDefault();
            Assert.NotNull(firstJobMethod);

            var mockTypeLocator = new Mock<ITypeLocator>(MockBehavior.Strict);
            mockTypeLocator.Setup(p => p.GetTypes()).Returns(new Type[] { type });
            var idProvider = new DefaultHostIdProvider(mockTypeLocator.Object);

            // as long as this test assembly name stays the same, the ID
            // computed should remain static. If this test is failing
            // it likely means we've changed the ID computation algorithm
            // which would be a BREAKING CHANGE
            string expected = "89810a97b95cb5c71e5b7915bb172e5d";

            var id = await idProvider.GetHostIdAsync(CancellationToken.None);
            Assert.Equal(expected, id);

            // ensure the same ID is returned each time
            mockTypeLocator = new Mock<ITypeLocator>(MockBehavior.Strict);
            mockTypeLocator.Setup(p => p.GetTypes()).Returns(new Type[] { type });
            idProvider = new DefaultHostIdProvider(mockTypeLocator.Object);
            Assert.Equal(expected, await idProvider.GetHostIdAsync(CancellationToken.None));

            // ensure once the ID is computed, a cached result is returned
            Assert.Equal(expected, await idProvider.GetHostIdAsync(CancellationToken.None));
            Assert.Equal(expected, await idProvider.GetHostIdAsync(CancellationToken.None));
            mockTypeLocator.Verify(p => p.GetTypes(), Times.Once);
        }

        [Fact]
        public async Task GetHostIdAsync_NoJobMethodsFound_ReturnsExpectedResult()
        {
            // pick a type that returns no job methods
            var type = typeof(FunctionNameAttribute);
            var firstJobMethod = FunctionIndexer.GetJobMethods(type).FirstOrDefault();
            Assert.Null(firstJobMethod);

            var mockTypeLocator = new Mock<ITypeLocator>(MockBehavior.Strict);
            mockTypeLocator.Setup(p => p.GetTypes()).Returns(new Type[] { type });
            var idProvider = new DefaultHostIdProvider(mockTypeLocator.Object);

            // as long as this test assembly name stays the same, the ID
            // computed should remain static. If this test is failing
            // it likely means we've changed the ID computation algorithm
            // which would be a BREAKING CHANGE
            string expected = "943b1888c56ce8f0c96b2e66e1c74a7e";

            var id = await idProvider.GetHostIdAsync(CancellationToken.None);
            Assert.Equal(expected, id);

            // ensure the same ID is returned each time
            mockTypeLocator = new Mock<ITypeLocator>(MockBehavior.Strict);
            mockTypeLocator.Setup(p => p.GetTypes()).Returns(new Type[] { type });
            idProvider = new DefaultHostIdProvider(mockTypeLocator.Object);
            Assert.Equal(expected, await idProvider.GetHostIdAsync(CancellationToken.None));
        }

        // This is a publically discoverable job function used by the test above
        public static void TestQueueFunction([FakeQueueTrigger] string message)
        {
        }
    }
}
