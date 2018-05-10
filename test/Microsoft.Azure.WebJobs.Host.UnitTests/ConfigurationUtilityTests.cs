// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class ConfigurationUtilityTests
    {
        [Fact]
        public void GetSettingFromConfigOrEnvironment_NotFound_ReturnsEmpty()
        {
            string value = ConfigurationUtility.GetSetting("DNE");
            Assert.Equal(null, value);
        }

        [Fact]
        public void GetSettingFromConfigOrEnvironment_NameNull_ReturnsEmpty()
        {
            string value = ConfigurationUtility.GetSetting(null);
            Assert.Equal(null, value);
        }

        [Fact]
        public void GetSettingFromConfigOrEnvironment_ConfigSetting_NoEnvironmentSetting()
        {
            string value = ConfigurationUtility.GetSetting("DisableSetting0");
            Assert.Equal("0", value);
        }

        [Fact]
        public void GetSettingFromConfigOrEnvironment_EnvironmentSetting_NoConfigSetting()
        {
            Environment.SetEnvironmentVariable("EnvironmentSetting", "1");

            string value = ConfigurationUtility.GetSetting("EnvironmentSetting");
            Assert.Equal("1", value);

            Environment.SetEnvironmentVariable("EnvironmentSetting", null);
        }

        [Fact]
        public void GetSettingFromConfigOrEnvironment_ConfigAndEnvironment_EnvironmentWins()
        {
            Environment.SetEnvironmentVariable("DisableSetting0", "1");

            string value = ConfigurationUtility.GetSetting("DisableSetting0");
            Assert.Equal("1", value);

            Environment.SetEnvironmentVariable("DisableSetting0", null);
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

                string result = ConfigurationUtility.GetSetting(lookup);
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
                bool isDisabled = ConfigurationUtility.IsSettingEnabled(settingName);

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
