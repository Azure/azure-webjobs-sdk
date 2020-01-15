// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication.ExtendedProtection;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[assembly: WebJobsStartup(typeof(Microsoft.Azure.WebJobs.Host.UnitTests.Hosting.WebJobsStartupTests.ExternalTestStartup))]

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
        public void UseWebJobsStartup_TestIConfiguration()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            TestLoggerProvider provider = new TestLoggerProvider();
            loggerFactory.AddProvider(provider);

            TestConfigurationStartup.Configuration = null;
            using (new StartupScope())
            {
                var builder = new HostBuilder()
                    .ConfigureWebJobs(webJobsBuilder =>
                    {
                        webJobsBuilder.UseWebJobsStartup(typeof(TestConfigurationStartup), NullLoggerFactory.Instance);
                    });

                IHost host = builder.Build();

                var configuration = host.Services.GetService<IConfiguration>();
                Assert.Same(configuration, TestConfigurationStartup.Configuration);
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

            var service = host.Services.GetService<TestExternalService>();
            Assert.NotNull(service);

            var messages = provider.GetAllLogMessages();
            Assert.NotEmpty(messages.Where(m => m.FormattedMessage.Contains("TestExternalService")));
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

            var service = host.Services.GetService<TestExternalService>();

            Assert.NotNull(service);
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

        private class TestConfigurationStartup : IWebJobsStartup
        {
            public TestConfigurationStartup(IConfiguration configuration)
            {
                Configuration = configuration;
            }

            public static IConfiguration Configuration { get; set; }

            public void Configure(IWebJobsBuilder builder)
            {
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
    }
}
