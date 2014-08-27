﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Xunit;
using Xunit.Extensions;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Bindings
{
    public class BindingDataPathTests
    {
        [Fact]
        public void CreateBindingData_IfExtensionSpecialScenario_ReturnsNamedParams()
        {
            var namedParams = BindingDataPath.CreateBindingData("input/{name}.csv", "input/a.b.csv");

            Assert.Equal("a.b", namedParams["name"]);
        }

        [Theory]
        [InlineData("input/{name}.{extension}", "input/foo.txt", "foo")]
        [InlineData("input/{name}.{extension}", "input/foo.bar.txt", "foo.bar")]
        [InlineData("input/foo/{name}.{extension}", "input/foo/foo.bar.txt", "foo.bar")]
        [InlineData("input/{name}/foo/{skip}.{extension}", "input/foo/foo/bar.txt", "foo")]
        public void CreateBindingData_IfNameExtensionCombination_ReturnsNamedParams(
            string pattern, string actualPath, string expectedName)
        {
            // Act
            var namedParams = BindingDataPath.CreateBindingData(pattern, actualPath);

            // Assert
            Assert.NotNull(namedParams);
            Assert.Equal(expectedName, namedParams["name"]);
            Assert.Equal("txt", namedParams["extension"]);
        }

        [Fact]
        public void CreateBindingData_IfExtensionLongest_WorksMultiple()
        {
            var namedParams = BindingDataPath.CreateBindingData("input/{name}.{extension}-/{other}", "input/foo.bar.txt-/asd-/ijij");

            Assert.Equal("foo.bar", namedParams["name"]);
            Assert.Equal("txt-/asd", namedParams["extension"]);
            Assert.Equal("ijij", namedParams["other"]);
        }

        [Fact]
        public void CreateBindingData_IfExtensionLongest_WorksMultiCharacterSeparator()
        {
            var namedParams = BindingDataPath.CreateBindingData("input/{name}.-/{extension}", "input/foo.-/bar.txt");

            Assert.Equal("foo", namedParams["name"]);
            Assert.Equal("bar.txt", namedParams["extension"]);
        }

        [Fact]
        public void CreateBindingData_IfNoMatch_ReturnsNull()
        {
            var namedParams = BindingDataPath.CreateBindingData("input/{name}.-/", "input/foo.bar.-/txt");

            Assert.Null(namedParams);
        }

        [Theory]
        [InlineData("input/{name}/foo/{skip}.{extension}", "input/name/foo/skip/bar.txt")]
        public void CreateBindingData_ShouldMatchBut_ReturnsNull(string pattern, string actualPath)
        {
            // Act
            var namedParams = BindingDataPath.CreateBindingData(pattern, actualPath);

            // Assert
            Assert.Null(namedParams);
        }

        [Theory]
        [InlineData(typeof(Int16), "-123")]
        [InlineData(typeof(Int32), "1234567890")]
        [InlineData(typeof(Int64), "123000000000000000")]
        [InlineData(typeof(UInt16), "123")]
        [InlineData(typeof(UInt32), "1234567890")]
        [InlineData(typeof(UInt64), "1230000000000000000")]
        [InlineData(typeof(Char), "R")]
        [InlineData(typeof(Byte), "255")]
        [InlineData(typeof(SByte), "-127")]
        public void ConvertParamValueToString_IfSupportedType_ReturnsStringValue(Type paramType, string expectedStringValue)
        {
            // Arrange
            var parseMethod = paramType.GetMethod("Parse", new Type[] { typeof(string) });
            object paramValue = parseMethod.Invoke(null, new object[] { expectedStringValue });

            // Act
            string stringParamValue = BindingDataPath.ConvertParameterValueToString(paramValue);

            // Assert
            Assert.NotNull(stringParamValue);
            Assert.Equal(expectedStringValue, stringParamValue);
        }

        [Fact]
        public void ConvertParamValueToString_IfStringParam_ReturnsStringValue()
        {
            // Arrange
            const string expectedStringValue = "Some random test string";

            // Act
            string stringParamValue = BindingDataPath.ConvertParameterValueToString(expectedStringValue);

            // Assert
            Assert.NotNull(stringParamValue);
            Assert.Equal(expectedStringValue, stringParamValue);
        }

        [Fact]
        public void ConvertParamValueToString_IfGuidParam_ReturnsStringValue()
        {
            // Arrange
            string expectedStringValue = "c914be08-fae6-4014-a619-c5f7ebf3fe37";
            Guid guidParam = Guid.Parse(expectedStringValue);

            // Act
            string stringParamValue = BindingDataPath.ConvertParameterValueToString(guidParam);

            // Assert
            Assert.NotNull(stringParamValue);
            Assert.Equal(expectedStringValue, stringParamValue);
        }

        [Fact]
        public void ConvertParamValueToString_IfUnupportedType_ReturnsNull()
        {
            // Arrange
            DateTime dateTimeParam = DateTime.Now;

            // Act
            string stringParamValue = BindingDataPath.ConvertParameterValueToString(dateTimeParam);

            // Assert
            Assert.Null(stringParamValue);
        }
    }
}
