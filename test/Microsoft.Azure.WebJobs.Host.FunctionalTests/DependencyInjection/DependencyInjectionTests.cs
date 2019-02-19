// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using Xunit;
using static Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonEnd2EndTests;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests.DependencyInjection
{
    public class DependencyInjectionTests
    {
        [Fact]
        public async Task AssertScopedServicesAreEqual()
        {
            var executionState = new ExecutionState();

            IHost host = new HostBuilder()
               .ConfigureDefaultTestHost<Program>()
               .ConfigureServices(services =>
               {
                   services.AddSingleton<IDistributedLockManager, FakeSingletonManager>();
                   services.AddTransient<IServiceA, ServiceA>();
                   services.AddTransient<IServiceB, ServiceB>();
                   services.AddScoped<ICommonService, CommonService>();
                   services.AddSingleton(executionState);
               })
               .Build();

            var jobHost = host.GetJobHost<Program>();
            await jobHost.CallAsync(nameof(Program.Func1), null);

            // Make sure the service was disposed
            Assert.True(executionState.ExecutionService.IsDisposed);
        }

        public class Program
        {
            private readonly IServiceA _serviceA;
            private readonly IServiceB _serviceB;
            private readonly ExecutionState _state;

            public Program(IServiceA serviceA, IServiceB serviceB, ICommonService commonService, ExecutionState state)
            {
                _serviceA = serviceA;
                _serviceB = serviceB;
                _state = state;
                _state.ExecutionService = serviceA.CommonService;
            }

            [NoAutomaticTrigger]
            [Singleton]
            public void Func1()
            {
                if (!ReferenceEquals(_serviceA.CommonService, _serviceB.CommonService) || !ReferenceEquals(_state.ExecutionService, _serviceA.CommonService))
                {
                    throw new Exception("Common services are not the same instance.");
                }
            }
        }

        public interface ICommonService : IDisposable
        {
            bool IsDisposed { get; }
        }

        public interface IServiceA
        {
            void Run();

            ICommonService CommonService { get; }
        }

        public interface IServiceB
        {
            void Run();

            ICommonService CommonService { get; }
        }

        public class ServiceA : IServiceA
        {
            public ServiceA(ICommonService commonService)
            {
                CommonService = commonService;
            }

            public ICommonService CommonService { get; }

            public void Run()
            {
                throw new NotImplementedException();
            }
        }

        public class ServiceB : IServiceB
        {
            public ServiceB(ICommonService commonService)
            {
                CommonService = commonService;
            }

            public ICommonService CommonService { get; }

            public void Run()
            {
                throw new NotImplementedException();
            }
        }

        public class CommonService : ICommonService
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        public class ExecutionState
        {
            public ICommonService ExecutionService { get; set; }
        }
    }
}
