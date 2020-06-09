// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class ConfigurationExtensionsTests
    {
        private readonly IConfiguration _configuration;

        public ConfigurationExtensionsTests()
        {
            _configuration = new ConfigurationBuilder()
                .Add(new WebJobsEnvironmentVariablesConfigurationSource())
                .Build();
        }

        [Theory]
        [InlineData("Foo__Bar__Baz", "Foo__Bar__Baz")]
        [InlineData("Foo__Bar__Baz", "foo__bar__baz")]
        [InlineData("Foo__Bar__Baz", "Foo:Bar:Baz")]
        [InlineData("Foo__Bar__Baz", "foo:bar:baz")]
        [InlineData("Foo:Bar:Baz", "Foo:Bar:Baz")]
        [InlineData("Foo:Bar:Baz", "foo:bar:baz")]
        [InlineData("Foo_Bar_Baz", "Foo_Bar_Baz")]
        [InlineData("Foo_Bar_Baz", "foo_bar_baz")]
        [InlineData("FooBarBaz", "FooBarBaz")]
        [InlineData("FooBarBaz", "foobarbaz")]
        public void GetSetting_NormalizesKeys(string key, string lookup)
        {
            try
            {
                string value = Guid.NewGuid().ToString();
                Environment.SetEnvironmentVariable(key, value);

                string result = _configuration[lookup];
                Assert.Equal(value, result);
            }
            finally
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }

        [Theory]
        [InlineData("True", true)]
        [InlineData("true", true)]
        [InlineData("1", true)]
        [InlineData("", false)]
        [InlineData("0", false)]
        [InlineData("false", false)]
        [InlineData("test", false)]
        public void IsSettingEnabled_ReturnsExpected(string settingValue, bool expectedResult)
        {
            // Arrange
            string settingName = "SettingEnabledTest";
            Environment.SetEnvironmentVariable(settingName, settingValue);

            try
            {
                // Act
                bool isDisabled = _configuration.IsSettingEnabled(settingName);

                // Assert
                Assert.True(isDisabled == expectedResult);
            }
            finally
            {
                // Clear
                Environment.SetEnvironmentVariable(settingName, null);
            }
        }
    }
}
