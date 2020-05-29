// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Hosting
{
    public class WebJobsConfigurationStartupTests
    {
        public WebJobsConfigurationStartupTests() { }

        [Fact]
        public void GenericUseWebJobsConfigurationStartup_CallsStartupMethods()
        {
            using (new StartupScope())
            {
                var builder = new HostBuilder()
                    .ConfigureWebJobs((c, b) => { }, o => { }, (c, configBuilder) =>
                     {
                         configBuilder.UseWebJobsConfigurationStartup<TestStartup1>();
                     });

                IHost host = builder.Build();

                Assert.Equal(1, TestStartup1.ConfigureInvoked);

                IConfiguration config = host.Services.GetService<IConfiguration>();

                Assert.Equal("123", config["abc"]);
            }
        }

        [Fact]
        public void UseWebJobsConfigurationStartup_CallsStartupMethods()
        {
            using (new StartupScope())
            {
                var builder = new HostBuilder()
                    .ConfigureWebJobs((c, b) => { }, o => { }, (c, configBuilder) =>
                    {
                        configBuilder.UseWebJobsConfigurationStartup(typeof(TestStartup1));
                    });


                IHost host = builder.Build();

                Assert.Equal(1, TestStartup1.ConfigureInvoked);

                IConfiguration config = host.Services.GetService<IConfiguration>();

                Assert.Equal("123", config["abc"]);
            }
        }

        [Fact]
        public void UseWebJobsConfigurationStartup_TestLogging()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            TestLoggerProvider provider = new TestLoggerProvider();
            loggerFactory.AddProvider(provider);

            using (new StartupScope())
            {
                var builder = new HostBuilder()
                    .ConfigureWebJobs((c, b) => { }, o => { }, (c, configBuilder) =>
                    {
                        configBuilder.UseWebJobsConfigurationStartup(typeof(TestStartup1), new WebJobsBuilderContext(), loggerFactory);
                        configBuilder.UseWebJobsConfigurationStartup(typeof(TestStartup2));
                    });

                IHost host = builder.Build();

                Assert.Equal(1, TestStartup1.ConfigureInvoked);
                Assert.Equal(1, TestStartup2.ConfigureInvoked);

                IConfiguration config = host.Services.GetService<IConfiguration>();
                Assert.Equal("000", config["abc"]);
                Assert.Equal("456", config["def"]);

                var messages = provider.GetAllLogMessages();
                Assert.Single(messages.Where(m => m.FormattedMessage.Contains(nameof(MemoryConfigurationSource))));
            }
        }

        [Fact]
        public void UseExternalConfigurationStartup_TestLogging()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            TestLoggerProvider provider = new TestLoggerProvider();
            loggerFactory.AddProvider(provider);

            var builder = new HostBuilder()
                .ConfigureWebJobs((c, b) => { }, o => { }, (c, configBuilder) =>
                    {
                        var context = new WebJobsBuilderContext();

                        // This will find ExternalTestStartupWithConfig in WebJobsStartupTests
                        configBuilder.UseExternalConfigurationStartup(new DefaultStartupTypeLocator(GetType().Assembly), context, NullLoggerFactory.Instance);
                        configBuilder.UseExternalConfigurationStartup(new DefaultStartupTypeLocator(GetType().Assembly), context, loggerFactory);
                        configBuilder.UseExternalConfigurationStartup(new DefaultStartupTypeLocator(GetType().Assembly), context, loggerFactory);
                    });

            IHost host = builder.Build();

            IConfiguration config = host.Services.GetService<IConfiguration>();
            Assert.Equal("123", config["abc"]);

            var messages = provider.GetAllLogMessages();
            Assert.Equal(2, messages.Count());
            Assert.Equal(messages.First().FormattedMessage, messages.Last().FormattedMessage);

            var messageLines = messages.First().FormattedMessage.Split(Environment.NewLine);
            Assert.Single(messageLines, m => m.Contains(nameof(MemoryConfigurationSource)));
            Assert.Single(messageLines, m => m.Contains(nameof(EnvironmentVariablesConfigurationSource)));
            Assert.Single(messageLines, m => m.Contains(nameof(JsonConfigurationSource))); // from AddTestSettings()
        }

        private class StartupScope : IDisposable
        {
            public StartupScope()
            {
                TestStartup1.Reset();
                TestStartup2.Reset();
            }

            public void Dispose()
            {
                TestStartup1.Reset();
                TestStartup2.Reset();
            }
        }

        private class TestStartup1 : IWebJobsConfigurationStartup
        {
            [ThreadStatic]
            private static int _configureInvoked;

            public static int ConfigureInvoked => _configureInvoked;

            public void Configure(WebJobsBuilderContext context, IWebJobsConfigurationBuilder builder)
            {
                IDictionary<string, string> data = new Dictionary<string, string>
                {
                    { "abc", "123" }
                };

                builder.ConfigurationBuilder.AddInMemoryCollection(data);

                _configureInvoked++;
            }

            internal static void Reset()
            {
                _configureInvoked = 0;
            }
        }

        private class TestStartup2 : IWebJobsConfigurationStartup
        {
            [ThreadStatic]
            private static int _configureInvoked;

            public static int ConfigureInvoked => _configureInvoked;

            internal static void Reset()
            {
                _configureInvoked = 0;
            }

            public void Configure(WebJobsBuilderContext context, IWebJobsConfigurationBuilder builder)
            {
                IDictionary<string, string> data = new Dictionary<string, string>
                {
                    { "abc", "000" },
                    { "def", "456" }
                };

                builder.ConfigurationBuilder.AddInMemoryCollection(data);

                _configureInvoked++;
            }
        }
    }
}
