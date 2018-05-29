// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Executors
{
#if false // $$$ Is this test still meaningful? 
    // Tests behavior when getting a storage account fails. 
    public class DynamicHostIdProviderTests
    {
        [Fact]
        public void GetHostIdAsync_IfStorageAccountProviderThrowsInvalidOperationException_WrapsException()
        {
            // Arrange
            Mock<XStorageAccountProvider> storageAccountProviderMock = new Mock<XStorageAccountProvider>(
                MockBehavior.Strict);
            
            InvalidOperationException innerException = new InvalidOperationException();
            storageAccountProviderMock
                .Setup(p => p.Get(It.IsAny<string>()))
                .Throws(innerException);
            var storageAccountProvider = storageAccountProviderMock.Object;

            IFunctionIndexProvider functionIndexProvider = CreateDummyFunctionIndexProvider();

            IHostIdProvider product = new DynamicHostIdProvider(storageAccountProvider, functionIndexProvider);
            CancellationToken cancellationToken = CancellationToken.None;

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => product.GetHostIdAsync(cancellationToken).GetAwaiter().GetResult());
            Assert.Equal("A host ID is required. Either set JobHostConfiguration.HostId or provide a valid storage " +
                "connection string.", exception.Message);
            Assert.Same(innerException, exception.InnerException);
        }

        private static IFunctionIndexProvider CreateDummyFunctionIndexProvider()
        {
            return new Mock<IFunctionIndexProvider>(MockBehavior.Strict).Object;
        }
    }
#endif
}
