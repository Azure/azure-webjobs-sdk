// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Hosting
{
    public class HostBuilderExtensionsTests
    {
        [Fact]
        public void ConfigureWebJobs_RegistersDefaultConfigSources()
        {
            IConfigurationBuilder configBuilder = null;

            var builder = new HostBuilder()
                .ConfigureAppConfiguration(b =>
                {
                    configBuilder = b;
                })
                .ConfigureWebJobs();

            IHost host = builder.Build();

            var configSources = configBuilder.Sources.ToArray();
            Assert.Equal(3, configSources.Length);

            var jsonConfigSource = (JsonConfigurationSource)configSources.OfType<JsonConfigurationSource>().Single();
            Assert.Equal("appsettings.json", jsonConfigSource.Path);
            Assert.True(jsonConfigSource.Optional);
            Assert.False(jsonConfigSource.ReloadOnChange);

            var envVarConfigSource = (EnvironmentVariablesConfigurationSource)configSources.OfType<EnvironmentVariablesConfigurationSource>().Single();
            Assert.Null(envVarConfigSource.Prefix);
        }

        [Fact]
        public void ConfigureWebJobs_DoesNotRegisterDefaultConfigSources_WhenAlreadyPresent()
        {
            IConfigurationBuilder configBuilder = null;

            var builder = new HostBuilder()
                .ConfigureAppConfiguration(b =>
                {
                    configBuilder = b;

                    // register our own and change defaults
                    b.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    b.AddEnvironmentVariables("custom");

                    Dictionary<string, string> inMemoryConfig = new Dictionary<string, string>();
                    b.AddInMemoryCollection(inMemoryConfig);
                })
                .ConfigureWebJobs();

            IHost host = builder.Build();

            var configSources = configBuilder.Sources.ToArray();
            Assert.Equal(4, configSources.Length);

            var jsonConfigSource = (JsonConfigurationSource)configSources.OfType<JsonConfigurationSource>().Single();
            Assert.Equal("appsettings.json", jsonConfigSource.Path);
            Assert.False(jsonConfigSource.Optional);
            Assert.True(jsonConfigSource.ReloadOnChange);

            var envVarConfigSource = (EnvironmentVariablesConfigurationSource)configSources.OfType<EnvironmentVariablesConfigurationSource>().Single();
            Assert.Equal("custom", envVarConfigSource.Prefix);
        }
    }
}
