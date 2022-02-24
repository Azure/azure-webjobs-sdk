// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Configuration
{
    public class WebJobsExtensionOptionsConfigurationTests
    {
        [Fact]
        public void Configuration_IsBound()
        {
            var config = new Dictionary<string, string>
            {
                { "AzureWebJobs:extensions:testext:testvalue1", "test1" },
                { "azurewebjobs:extensions:testext:testvalue2", "test2" },
            };

            TestOptions options = ConfigureOptions<TestOptions>(config, "testext");

            // Assert
            Assert.Equal("test1", options.TestValue1);
            Assert.Equal("test2", options.TestValue2);
        }

        [Fact]
        public void Configuration_WithConfiguredRootSectionName_IsBound()
        {
            var config = new Dictionary<string, string>
            {
                { "AzureWebJobsConfigurationSection", "testroot" },
                { "testroot:extensions:testext:testvalue1", "test1" },
                { "testroot:extensions:testext:testvalue2", "test2" },
            };

            TestOptions options = ConfigureOptions<TestOptions>(config, "testext");

            // Assert
            Assert.Equal("test1", options.TestValue1);
            Assert.Equal("test2", options.TestValue2);
        }

        [Fact]
        public void Configuration_WithEmptyConfiguredRootSectionName_IsBound()
        {
            var config = new Dictionary<string, string>
            {
                { "AzureWebJobsConfigurationSection", string.Empty },
                { "extensions:testext:testvalue1", "test1" },
                { "extensions:testext:testvalue2", "test2" },
            };

            TestOptions options = ConfigureOptions<TestOptions>(config, "testext");

            // Assert
            Assert.Equal("test1", options.TestValue1);
            Assert.Equal("test2", options.TestValue2);
        }

        private TOptions ConfigureOptions<TOptions>(IDictionary<string, string> configValues, string extensionName = null)
            where TOptions : class, new()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues);

            var optionsConfig = new WebJobsExtensionOptionsConfiguration<TOptions>(config.Build(), extensionName, (s, p, o) => s.GetSection(p).Bind(o));
            var options = new TOptions();

            // Act
            optionsConfig.Configure(options);

            return options;
        }

        private class TestOptions
        {
            public string TestValue1 { get; set; }

            public string TestValue2 { get; set; }
        }
    }
}
