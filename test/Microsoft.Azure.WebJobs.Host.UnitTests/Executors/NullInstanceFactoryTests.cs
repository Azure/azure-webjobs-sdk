// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Executors
{
    public class NullInstanceFactoryTests
    {
        [Fact]
        public void Create_ReturnsNull()
        {
            // Arrange
            IJobInstanceFactory<object> product = CreateProductUnderTest<object>();
            var functionInstanceMock = new Mock<IFunctionInstanceEx>();

            // Act
            object instance = product.Create(functionInstanceMock.Object);

            // Assert
            Assert.Null(instance);
        }

        private static NullInstanceFactory<TReflected> CreateProductUnderTest<TReflected>()
        {
            return NullInstanceFactory<TReflected>.Instance;
        }
    }
}
