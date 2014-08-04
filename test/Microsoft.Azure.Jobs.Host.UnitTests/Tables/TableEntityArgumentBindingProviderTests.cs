﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Jobs.Host.Bindings;
using Microsoft.Azure.Jobs.Host.Tables;
using Microsoft.WindowsAzure.Storage.Table;
using Xunit;

namespace Microsoft.Azure.Jobs.Host.UnitTests.Tables
{
    public class TableEntityArgumentBindingProviderTests
    {
        [Fact]
        public void Create_ReturnsNull_IfByRefParameter()
        {
            // Arrange
            ITableEntityArgumentBindingProvider product = new TableEntityArgumentBindingProvider();

            Type parameterType = typeof(SimpleTableEntity).MakeByRefType();

            // Act
            IArgumentBinding<TableEntityContext> binding = product.TryCreate(parameterType);

            // Assert
            Assert.Null(binding);
        }

        [Fact]
        public void Create_ReturnsNull_IfContainsUnresolvedGenericParameter()
        {
            // Arrange
            ITableEntityArgumentBindingProvider product = new TableEntityArgumentBindingProvider();

            Type parameterType = typeof(GenericClass<>);

            // Act
            IArgumentBinding<TableEntityContext> binding = product.TryCreate(parameterType);

            // Assert
            Assert.Null(binding);
        }

        [Fact]
        public void Create_ReturnsBinding_IfContainsResolvedGenericParameter()
        {
            // Arrange
            ITableEntityArgumentBindingProvider product = new TableEntityArgumentBindingProvider();

            Type parameterType = typeof(GenericClass<int>);

            // Act
            IArgumentBinding<TableEntityContext> binding = product.TryCreate(parameterType);

            // Assert
            Assert.NotNull(binding);
        }

        private class SimpleTableEntity : TableEntity
        {
        }

        private class GenericClass<TArgument> : TableEntity
        {
        }
    }
}
