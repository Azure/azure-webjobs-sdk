// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Microsoft.Azure.WebJobs.Hosting;
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
                        { "AzureWebJobs:extensions:eventHubs:config1", "test1" },
                        { "AzureWebJobs:extensions:noInterface:config1", "test1" }
                    });
                })
                .ConfigureDefaultTestHost(b =>
                {
                    b.AddExtension<TestExtensionConfigProvider>()
                    .BindOptions<TestOptions>();

                    b.AddExtension<TestEventHubExtensionConfigProvider>()
                    .BindOptions<TestEventHubOptions>();

                    b.AddExtension<TestNoInterfaceExtensionConfigProvider>()
                    .BindOptions<TestNoInterfaceOptions>();

                    b.AddExtension<TestExtensionConfigProvider>()
                    .BindOptions<TestOptions>()
                    .BindOptions<TestOptions>();
                }).Build();
            
            var extensionOptionsProvider = host.Services.GetServices<IExtensionOptionsProvider>().ToArray();
            Assert.Equal(3, extensionOptionsProvider.Count());
            Assert.Equal("Test", extensionOptionsProvider[0].ExtensionInfo.ConfigurationSectionName);
            Assert.Equal("test1", ((TestOptions)extensionOptionsProvider[0].GetOptions()).Config1);
            Assert.Equal("EventHubs", extensionOptionsProvider[1].ExtensionInfo.ConfigurationSectionName);
            Assert.Equal("test1", ((TestEventHubOptions)extensionOptionsProvider[1].GetOptions()).Config1);
            Assert.Equal("NoInterface", extensionOptionsProvider[2].ExtensionInfo.ConfigurationSectionName);
            Assert.Equal("test1", ((TestNoInterfaceOptions)extensionOptionsProvider[2].GetOptions()).Config1);
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

        [Extension("NoInterface", "NoInterface")]
        private class TestNoInterfaceExtensionConfigProvider : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
            }
        }

        private class TestNoInterfaceOptions
        {
            public string Config1 { get; set; }

            public string Config2 { get; set; }

            public string Config3 { get; set; }
        }
    }
}
