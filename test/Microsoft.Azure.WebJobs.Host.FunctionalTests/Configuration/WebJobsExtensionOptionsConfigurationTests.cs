// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;
using Microsoft.Azure.WebJobs.Hosting;
using Xunit.Sdk;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Description;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests.Configuration
{
    public class WebJobsExtensionOptionsConfigurationTests
    {
        [Fact]
        public void BindOptions_BindsToConfiguration()
        {
            var host = new HostBuilder()
                .ConfigureAppConfiguration(b =>
                {
                    b.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "AzureWebJobs:extensions:test:config1", "test1" },
                        { "AzureWebJobs:extensions:test:config2", "test2" }
                    });
                })
                .ConfigureDefaultTestHost(b =>
                {
                    b.AddExtension<TestExtensionConfigProvider>()
                    .BindOptions<TestOptions>();
                }).Build();

            var options = host.Services.GetService<IOptions<TestOptions>>();

            Assert.Equal("test1", options.Value.Config1);
            Assert.Equal("test2", options.Value.Config2);
        }


        [Fact]
        public void ConfigureOptions_BindsToConfiguration()
        {
            var host = new HostBuilder()
                .ConfigureAppConfiguration(b =>
                {
                    b.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "AzureWebJobs:extensions:test:config1", "test1" },
                        { "AzureWebJobs:extensions:test:config2", "test2" },
                        { "AzureWebJobs:extensions:test:config3", "test3" }
                    });
                })
                .ConfigureDefaultTestHost(b =>
                {
                    b.AddExtension<TestExtensionConfigProvider>()
                    .ConfigureOptions<TestOptions>((section, o) =>
                    {
                        section.Bind(o);
                        o.Config3 = "fromconfigureoptions";
                    });
                }).Build();

            var options = host.Services.GetService<IOptions<TestOptions>>();

            Assert.Equal("test1", options.Value.Config1);
            Assert.Equal("test2", options.Value.Config2);
            Assert.Equal("fromconfigureoptions", options.Value.Config3);
        }

        [Fact]
        public void ConfigureAndBindOptions_BindsToConfiguration()
        {
            var host = new HostBuilder()
                .ConfigureAppConfiguration(b =>
                {
                    b.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "AzureWebJobs:extensions:test:config1", "test1" },
                        { "AzureWebJobs:extensions:test:config2", "test2" },
                        { "AzureWebJobs:extensions:test:config3", "test3" }
                    });
                })
                .ConfigureDefaultTestHost(b =>
                {
                    b.AddExtension<TestExtensionConfigProvider>()
                    .BindOptions<TestOptions>()
                    .ConfigureOptions<TestOptions>((section, o) =>
                    {
                        o.Config3 = "fromconfigureoptions";
                    });
                }).Build();

            var options = host.Services.GetService<IOptions<TestOptions>>();
            Assert.Equal("test1", options.Value.Config1);
            Assert.Equal("test2", options.Value.Config2);
            Assert.Equal("fromconfigureoptions", options.Value.Config3);
        }

        [Fact]
        public void ConfigureExtensionOptionsInfo_BindToEnumeration()
        {
            var host = new HostBuilder()
                .ConfigureAppConfiguration(b =>
                {
                    b.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "AzureWebJobs:extensions:test:config1", "test1" },
                        { "AzureWebJobs:extensions:test:config2", "test2" },
                        { "AzureWebJobs:extensions:test:config3", "test3" },
                        { "AzureWebJobs:extensions:eventHubs:config1", "test1" },
                        { "AzureWebJobs:extensions:eventHubs:config2", "test2" },
                        { "AzureWebJobs:extensions:eventHubs:config3", "test3" },
                    });
                })
                .ConfigureDefaultTestHost(b =>
                {
                    b.AddExtension<TestExtensionConfigProvider>()
                    .BindOptions<TestOptions>();

                    b.AddExtension<TestEventHubExtensionConfigProvider>()
                    .BindOptions<TestEventHubOptions>();

                    b.AddExtension<TestIgnoredExtensionConfigProvider>()
                    .BindOptions<TestIgnoredOptions>();
                }).Build();
            
            var extensionOptionsInfo = host.Services.GetServices<IExtensionOptionsInfo>().ToArray();
            Assert.Equal(2, extensionOptionsInfo.Count());
            Assert.Equal("Test", extensionOptionsInfo[0].ExtensionInfo.ConfigurationSectionName);
            Assert.Equal("test1", ((TestOptions)extensionOptionsInfo[0].OptionsFormatter).Config1);
            Assert.Equal("EventHubs", extensionOptionsInfo[1].ExtensionInfo.ConfigurationSectionName);
            Assert.Equal("test1", ((TestEventHubOptions)extensionOptionsInfo[1].OptionsFormatter).Config1);
        }

        private class TestExtensionConfigProvider : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
            }
        }
        
        private class TestOptions : IOptionsFormatter
        {
            public string Config1 { get; set; }

            public string Config2 { get; set; }

            public string Config3 { get; set; }
            public string Format()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        [Extension("EventHubs", "EventHubs")]
        private class TestEventHubExtensionConfigProvider : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
            }
        }

        private class TestEventHubOptions : IOptionsFormatter
        {
            public string Config1 { get; set; }

            public string Config2 { get; set; }

            public string Config3 { get; set; }

            public string Format()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        [Extension("Ignored")]
        private class TestIgnoredExtensionConfigProvider : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
            }
        }

        private class TestIgnoredOptions
        {
            public string Config1 { get; set; }

            public string Config2 { get; set; }

            public string Config3 { get; set; }
        }
    }
}
