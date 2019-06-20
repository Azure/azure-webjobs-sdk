// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests.DependencyInjection
{
    public class MethodDependencyInjectionTests
    {
        [Fact]
        public async Task AssertInvalidServiceConfiguration()
        {
            var expectedMessage = $"No service for type '{typeof(MethodDependencyInjectionTests).FullName}+{nameof(IServiceA)}' has been registered.";
            var host = new HostBuilder()
                .ConfigureDefaultTestHost<Program>()
                .Build();
            var jobHost = host.GetJobHost<Program>();
            var functionInvocationException = await Assert.ThrowsAsync<FunctionInvocationException>(async () => await jobHost.CallAsync(nameof(Program.Func1), null));
            var baseException = functionInvocationException.GetBaseException();

            Assert.NotNull(baseException);
            Assert.IsType<InvalidOperationException>(baseException);
            Assert.Equal(expectedMessage, baseException.Message);
        }

        [Fact]
        public async Task AssertSuccessfulMethodInjectionWithNoParameters()
        {
            var host = new HostBuilder()
                .ConfigureDefaultTestHost<Program>()
                .ConfigureServices(services => { services.AddSingleton<IServiceA, ServiceA1>(); })
                .Build();
            var jobHost = host.GetJobHost<Program>();
            await jobHost.CallAsync(nameof(Program.Func0), null);
        }

        [Fact]
        public async Task AssertSuccessfulMethodInjectionWithOneInjectedService()
        {
            var host = new HostBuilder()
                .ConfigureDefaultTestHost<Program>()
                .ConfigureServices(services => { services.AddSingleton<IServiceA, ServiceA1>(); })
                .Build();
            var jobHost = host.GetJobHost<Program>();
            await jobHost.CallAsync(nameof(Program.Func1), null);
        }

        [Fact]
        public async Task AssertSuccessfulMethodInjectionWithOneInjectedServiceAndARuntimeArgument()
        {
            var host = new HostBuilder()
                .ConfigureDefaultTestHost<Program>()
                .ConfigureServices(services => { services.AddSingleton<IServiceA, ServiceA1>(); })
                .Build();
            var jobHost = host.GetJobHost<Program>();
            var arguments = new Dictionary<string, object>
            {
                {"valueB", 4}
            };
            await jobHost.CallAsync(nameof(Program.Func2), arguments);
        }

        [Fact]
        public async Task AssertSuccessfulMethodInjectionWithTwoParametersOfTheSameService()
        {
            var host = new HostBuilder()
                .ConfigureDefaultTestHost<Program>()
                .ConfigureServices(services => { services.AddSingleton<IServiceA, ServiceA1>(); })
                .Build();
            var jobHost = host.GetJobHost<Program>();
            await jobHost.CallAsync(nameof(Program.Func3), null);
        }

        [Fact]
        public async Task AssertSuccessfulMethodInjectionWithIEnumerableOfService()
        {
            var host = new HostBuilder()
                .ConfigureDefaultTestHost<Program>()
                .ConfigureServices(services => { services.AddSingleton<IServiceA, ServiceA1>(); })
                .ConfigureServices(services => { services.AddSingleton<IServiceA, ServiceA2>(); })
                .Build();
            var jobHost = host.GetJobHost<Program>();
            await jobHost.CallAsync(nameof(Program.Func4), null);
        }

        [Fact]
        public async Task AssertSuccessfulMethodInjectionWithRuntimeArgumentTakingPrecedenceOverInjectedService()
        {
            var host = new HostBuilder()
                .ConfigureDefaultTestHost<Program>()
                .ConfigureServices(services => { services.AddSingleton<IServiceA, ServiceA1>(); })
                .Build();
            var jobHost = host.GetJobHost<Program>();
            var arguments = new Dictionary<string, object>
            {
                {"serviceA", new ServiceA2()}
            };
            await jobHost.CallAsync(nameof(Program.Func5), arguments);
        }

        [Fact]
        public async Task AssertSuccessfulMethodInjectionWithServiceWhenRuntimeArgumentIsRightTypeButWrongName()
        {
            var host = new HostBuilder()
                .ConfigureDefaultTestHost<Program>()
                .ConfigureServices(services => { services.AddSingleton<IServiceA, ServiceA1>(); })
                .Build();
            var jobHost = host.GetJobHost<Program>();
            var arguments = new Dictionary<string, object>
            {
                {"serviceA2", new ServiceA2()}
            };
            await jobHost.CallAsync(nameof(Program.Func6), arguments);
        }

        #region TestTypes

        public class Program
        {
            [NoAutomaticTrigger]
            [Singleton]
            public void Func0()
            {
            }

            [NoAutomaticTrigger]
            [Singleton]
            public void Func1(IServiceA serviceA)
            {
                Assert.NotNull(serviceA);
                Assert.Equal("A1", serviceA.Run());
            }

            [NoAutomaticTrigger]
            [Singleton]
            public void Func2(IServiceA serviceA, int valueB)
            {
                Assert.NotNull(serviceA);
                Assert.Equal(4, valueB);
            }

            [NoAutomaticTrigger]
            [Singleton]
            public void Func3(IServiceA service1, IServiceA service2)
            {
                Assert.NotNull(service1);
                Assert.NotNull(service2);
                Assert.Equal("A1", service1.Run());
                Assert.Equal(service1, service2);
            }

            [NoAutomaticTrigger]
            [Singleton]
            public void Func4(IEnumerable<IServiceA> serviceAs)
            {
                Assert.NotNull(serviceAs);
                var services = serviceAs.ToArray();
                Assert.Equal(2, services.Length);
                Assert.Equal("A1", services[0].Run());
                Assert.Equal("A2", services[1].Run());
            }

            [NoAutomaticTrigger]
            [Singleton]
            public void Func5(IServiceA serviceA)
            {
                Assert.NotNull(serviceA);
                Assert.Equal("A2", serviceA.Run());
            }

            [NoAutomaticTrigger]
            [Singleton]
            public void Func6(IServiceA serviceA)
            {
                Assert.NotNull(serviceA);
                Assert.Equal("A1", serviceA.Run());
            }
        }

        public interface IServiceA
        {
            string Run();
        }

        public class ServiceA1 : IServiceA
        {
            public string Run()
            {
                return "A1";
            }
        }

        public class ServiceA2 : IServiceA
        {
            public string Run()
            {
                return "A2";
            }
        }

        #endregion
    }
}