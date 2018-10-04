// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Executors
{
    public class DefaultJobActivatorTests
    {
        [Fact]
        public void Create_ReturnsNonNull()
        {
            // Arrange
            IJobActivator product = CreateProductUnderTest();

            // Act
            object instance = product.CreateInstance<object>();

            // Assert
            Assert.NotNull(instance);
        }

        [Fact]
        public void Create_ReturnsNewInstance()
        {
            // Arrange
            IJobActivator product = CreateProductUnderTest();
            object originalInstance = product.CreateInstance<object>();

            // Act
            object instance = product.CreateInstance<object>();

            // Assert
            Assert.NotNull(instance);
            Assert.NotSame(originalInstance, instance);
        }

        [Fact]
        public void Create_InjectsDependencies()
        {
            // Arrange
            IJobActivator product = CreateProductUnderTest();

            // Act
            var instance = product.CreateInstance<SampleFunctions>();

            // Assert
            Assert.NotNull(instance.SampleServiceA);
            Assert.NotNull(instance.SampleServiceB);
        }

        private static DefaultJobActivator CreateProductUnderTest()
        {
            var hostBuilder = new HostBuilder();
            hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<ISampleServiceA, SampleServiceA>();
                services.AddSingleton<ISampleServiceB, SampleServiceB>();
            });
            var host = hostBuilder.Build();
            var serviceProvider = (IServiceProvider)host.Services.GetService(typeof(IServiceProvider));
            return new DefaultJobActivator(serviceProvider);
        }

        public class SampleFunctions
        {
            public SampleFunctions(ISampleServiceA sampleServiceA, ISampleServiceB sampleServiceB)
            {
                SampleServiceA = sampleServiceA;
                SampleServiceB = sampleServiceB;
            }

            public ISampleServiceA SampleServiceA { get;  }

            public ISampleServiceB SampleServiceB { get; }
        }

        public interface ISampleServiceA
        {
            void DoIt();
        }

        public class SampleServiceA : ISampleServiceA
        {
            private readonly ILogger _logger;

            public SampleServiceA(ILogger<SampleServiceA> logger)
            {
                _logger = logger;
            }

            public void DoIt()
            {
                _logger.LogInformation("SampleServiceA.DoIt invoked!");
            }
        }

        public interface ISampleServiceB
        {
            void DoIt();
        }

        public class SampleServiceB : ISampleServiceB
        {
            private readonly ILogger _logger;

            public SampleServiceB(ILogger<SampleServiceB> logger)
            {
                _logger = logger;
            }

            public void DoIt()
            {
                _logger.LogInformation("SampleServiceB.DoIt invoked!");
            }
        }
    }
}
