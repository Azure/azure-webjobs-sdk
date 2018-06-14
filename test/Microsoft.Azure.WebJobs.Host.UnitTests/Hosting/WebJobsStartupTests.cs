// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

[assembly: WebJobsStartup(typeof(Microsoft.Azure.WebJobs.Host.UnitTests.Hosting.WebJobsStartupTests.ExternalTestStartup))]

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Hosting
{
    public class WebJobsStartupTests
    {
        [Fact]
        public void GenericUseWebJobsStartup_CallsStartupMethods()
        {
            using (new StartupScope())
            {
                var builder = new HostBuilder()
                    .UseWebJobsStartup<TestStartup>();

                Assert.True(TestStartup.ConfigureInvoked);

                IHost host = builder.Build();

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
                    .UseWebJobsStartup(typeof(TestStartup));

                Assert.True(TestStartup.ConfigureInvoked);

                IHost host = builder.Build();

                ITestService service = host.Services.GetService<ITestService>();

                Assert.NotNull(service);
            }
        }

        [Fact]
        public void StartupTypes_FromAttributes_AreConfigured()
        {
            var builder = new HostBuilder()
                  .UseExternalStartup(new DefaultStartupTypeDiscoverer(GetType().Assembly));

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

            public void Configure(IHostBuilder builder)
            {
                builder.ConfigureServices(c => c.AddSingleton<ITestService, TestService>());

                _configureInvoked = true;
            }

            internal static void Reset()
            {
                _configureInvoked = false;
            }
        }


        public class ExternalTestStartup : IWebJobsStartup
        {
            public void Configure(IHostBuilder builder)
            {
                builder.ConfigureServices(c => c.AddSingleton<TestExternalService>());
            }
        }

        private interface ITestService { }

        private class TestService : ITestService { }

        private class TestExternalService { }
    }
}
