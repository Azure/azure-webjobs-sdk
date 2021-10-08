// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class ConfigurationExtensionsTests
    {
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

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            try
            {
                // Act
                bool isDisabled = configuration.IsSettingEnabled(settingName);

                // Assert
                Assert.True(isDisabled == expectedResult);
            }
            finally
            {
                // Clear
                Environment.SetEnvironmentVariable(settingName, null);
            }
        }

        [Fact]
        public void GetWebJobsConnectionSection_ReturnsExpected()
        {
            // Value and children in the section
            var configValues = new Dictionary<string, string>
            {
                { "AzureWebJobsStorage", "connectionString" },
                { "AzureWebJobsStorage:subsection", "test1" },
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            var section = config.GetWebJobsConnectionSection(ConnectionStringNames.Storage);
            Assert.True(section.Exists());
            Assert.False(string.IsNullOrEmpty(section.Value));
            Assert.Equal("test1", section["subsection"]);

            // No value, just children
            configValues = new Dictionary<string, string>
            {
                { "AzureWebJobsStorage:subsection", "test2" },
            };
            config = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            section = config.GetWebJobsConnectionSection(ConnectionStringNames.Storage);
            Assert.True(section.Exists());
            Assert.True(string.IsNullOrEmpty(section.Value));
            Assert.Equal("test2", section["subsection"]);
        }
    }
}
