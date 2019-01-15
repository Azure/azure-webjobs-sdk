﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.TestHelpers;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Protocols
{
    public class JsonTypeNameAttributeTests
    {
        [Fact]
        public static void Constructor_IfTypeNameIsNull_Throws()
        {
            // Arrange
            string typeName = null;

            // Act & Assert
            ExceptionAssert.ThrowsArgumentNull(() => CreateProductUnderTest(typeName), "typeName");
        }

        [Fact]
        public static void TypeName_IsSpecifiedInstance()
        {
            // Arrange
            string expectedTypeName = "IgnoreName";
            JsonTypeNameAttribute product = CreateProductUnderTest(expectedTypeName);

            // Act
            string typeName = product.TypeName;

            // Assert
            Assert.Same(expectedTypeName, typeName);
        }

        private static JsonTypeNameAttribute CreateProductUnderTest(string typeName)
        {
            return new JsonTypeNameAttribute(typeName);
        }
    }
}
