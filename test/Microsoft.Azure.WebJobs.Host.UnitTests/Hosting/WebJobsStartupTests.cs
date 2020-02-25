// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[assembly: WebJobsStartup(typeof(Microsoft.Azure.WebJobs.Host.UnitTests.Hosting.WebJobsStartupTests.ExternalTestStartup))]
[assembly: WebJobsStartup(typeof(Microsoft.Azure.WebJobs.Host.UnitTests.Hosting.WebJobsStartupTests.ExternalTestStartupWithConfig))]

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Hosting
{
    public class WebJobsStartupTests
    {
        public WebJobsStartupTests() { }

        [Fact]
        public void WebJobsStartupAttribute_Constructor_InitializesAlias()
        {
            var attribute = new WebJobsStartupAttribute(typeof(FooStartup));
            Assert.Equal("Foo", attribute.Name);

            attribute = new WebJobsStartupAttribute(typeof(FooWebJobsStartup));
            Assert.Equal("Foo", attribute.Name);

            attribute = new WebJobsStartupAttribute(typeof(FooWebJobsStartup), "Bar");
            Assert.Equal("Bar", attribute.Name);
        }

        [Fact]
        public void GenericUseWebJobsStartup_CallsStartupMethods()
        {
            using (new StartupScope())
            {
                var builder = new HostBuilder()
                    .ConfigureWebJobs(webJobsBuilder =>
                    {
                        webJobsBuilder.UseWebJobsStartup<TestStartup>();
                    });

                IHost host = builder.Build();

                Assert.True(TestStartup.ConfigureInvoked);

                ITestService service = host.Services.GetService<ITestService>();

                Assert.NotNull(service);
            }
        }

        [Fact]
        public void UseWebJobsStartup_CallsStartupMethods()
        {
            using (new StartupScope())
            {
                var builder = new HostBuilder()
                    .ConfigureWebJobs(webJobsBuilder =>
                    {
                        webJobsBuilder.UseWebJobsStartup(typeof(TestStartup));
                    });


                IHost host = builder.Build();

                Assert.True(TestStartup.ConfigureInvoked);

                ITestService service = host.Services.GetService<ITestService>();

                Assert.NotNull(service);
            }
        }

        [Fact]
        public void UseWebJobsStartup_TestLogging()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            TestLoggerProvider provider = new TestLoggerProvider();
            loggerFactory.AddProvider(provider);

            using (new StartupScope())
            {
                var builder = new HostBuilder()
                    .ConfigureWebJobs(webJobsBuilder =>
                    {
                        webJobsBuilder.UseWebJobsStartup(typeof(TestStartup), NullLoggerFactory.Instance);
                        webJobsBuilder.UseWebJobsStartup(typeof(TestLoggingStartup), loggerFactory);
                    });

                IHost host = builder.Build();

                Assert.True(TestStartup.ConfigureInvoked);
                ITestService service = host.Services.GetService<ITestService>();
                Assert.NotNull(service);

                ITestLoggingService loggingService = host.Services.GetService<ITestLoggingService>();
                Assert.NotNull(loggingService);

                var messages = provider.GetAllLogMessages();
                Assert.NotEmpty(messages.Where(m => m.FormattedMessage.Contains("ITestLoggingService")));
            }
        }

        [Fact]
        public void UseExternalStartup_TestLogging()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            TestLoggerProvider provider = new TestLoggerProvider();
            loggerFactory.AddProvider(provider);

            var builder = new HostBuilder()
               .ConfigureWebJobs(webJobsBuilder =>
               {
                   webJobsBuilder.UseExternalStartup(new DefaultStartupTypeLocator(GetType().Assembly), NullLoggerFactory.Instance);
                   webJobsBuilder.UseExternalStartup(new DefaultStartupTypeLocator(GetType().Assembly), loggerFactory);
               });

            IHost host = builder.Build();

            Assert.NotNull(host.Services.GetService<TestExternalService>());
            Assert.NotNull(host.Services.GetService<TestExternalServiceWithConfig>());

            var messages = provider.GetAllLogMessages();
            Assert.NotEmpty(messages.Where(m => m.FormattedMessage.Contains("TestExternalService:")));
            Assert.NotEmpty(messages.Where(m => m.FormattedMessage.Contains("TestExternalServiceWithConfig:")));
        }

        [Fact]
        public void StartupTypes_FromAttributes_AreConfigured()
        {
            var builder = new HostBuilder()
                .ConfigureWebJobs(webJobsBuilder =>
                {
                    webJobsBuilder.UseExternalStartup(new DefaultStartupTypeLocator(GetType().Assembly), NullLoggerFactory.Instance);
                });

            IHost host = builder.Build();

            Assert.NotNull(host.Services.GetService<TestExternalService>());
            Assert.NotNull(host.Services.GetService<TestExternalServiceWithConfig>());
        }

        private class StartupScope : IDisposable
        {
            public StartupScope()
            {
                TestStartup.Reset();
            }

            public void Dispose()
            {
                TestStartup.Reset();
            }
        }

        private class TestStartup : IWebJobsStartup
        {
            [ThreadStatic]
            private static bool _configureInvoked;

            public static bool ConfigureInvoked => _configureInvoked;

            public void Configure(IWebJobsBuilder builder)
            {
                builder.Services.AddSingleton<ITestService, TestService>();

                _configureInvoked = true;
            }

            internal static void Reset()
            {
                _configureInvoked = false;
            }
        }

        private class TestLoggingStartup : IWebJobsStartup
        {
            public void Configure(IWebJobsBuilder builder)
            {
                builder.Services.AddSingleton<ITestLoggingService, TestLoggingService>();
            }
        }

        public class ExternalTestStartup : IWebJobsStartup
        {
            public void Configure(IWebJobsBuilder builder)
            {
                builder.Services.AddSingleton<TestExternalService>();
            }
        }

        public class ExternalTestStartupWithConfig : IWebJobsStartup, IWebJobsConfigurationStartup
        {
            public void Configure(IWebJobsBuilder builder)
            {
                builder.Services.AddSingleton<TestExternalServiceWithConfig>();
            }

            public void Configure(WebJobsBuilderContext context, IWebJobsConfigurationBuilder builder)
            {
                IDictionary<string, string> data = new Dictionary<string, string>
                {
                    { "abc", "123" }
                };

                builder.ConfigurationBuilder.AddInMemoryCollection(data);
                builder.ConfigurationBuilder.AddEnvironmentVariables();
                builder.ConfigurationBuilder.AddTestSettings();
            }
        }

        public class ExternalTestStartupWithConfig : IWebJobsStartup, IWebJobsConfigurationStartup
        {
            public void Configure(IWebJobsBuilder builder)
            {
                builder.Services.AddSingleton<TestExternalServiceWithConfig>();
            }

            public void Configure(IWebJobsConfigurationBuilder builder)
            {
                IDictionary<string, string> data = new Dictionary<string, string>
                {
                    { "abc", "123" }
                };

                builder.ConfigurationBuilder.AddInMemoryCollection(data);
                builder.ConfigurationBuilder.AddEnvironmentVariables();
                builder.ConfigurationBuilder.AddTestSettings();
            }
        }

        public class FooStartup : IWebJobsStartup
        {
            public void Configure(IWebJobsBuilder builder)
            {
                throw new NotImplementedException();
            }
        }

        public class FooWebJobsStartup : IWebJobsStartup
        {
            public void Configure(IWebJobsBuilder builder)
            {
                throw new NotImplementedException();
            }
        }

        private interface ITestService { }

        private interface ITestLoggingService { }

        private class TestService : ITestService { }

        private class TestLoggingService : ITestLoggingService
        {
            public string Name = "testLogging";
        }

        private class TestExternalService { }

        private class TestExternalServiceWithConfig { } // use when also registering config
    }
}
