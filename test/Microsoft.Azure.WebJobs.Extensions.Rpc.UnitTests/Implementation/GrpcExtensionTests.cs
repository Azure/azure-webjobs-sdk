// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Microsoft.Azure.WebJobs.Extensions.Rpc.TestService;

namespace Microsoft.Azure.WebJobs.Extensions.Rpc.Implementation.UnitTests
{
    public class GrpcExtensionTests
    {
        private readonly IServiceProvider _serviceProvider;

        public GrpcExtensionTests()
        {
            ServiceCollection services = new();
            services
                .AddLogging()
                .AddRouting()
                .AddGrpc();
            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public void Apply_AddsGrpc()
        {
            GrpcExtension<Service> extension = new(ExtensionInfo.FromExtension<TestExtension>());
            TestEndpointRouteBuilder builder = new(_serviceProvider);
            extension.Apply(builder, NullLogger.Instance);

            Assert.Single(builder.DataSources);

            EndpointDataSource source = builder.DataSources.First();
            Assert.Equal(3, source.Endpoints.Count);

            foreach ((string actual, string expected) in source.Endpoints.Select(x => x.DisplayName)
                .Zip(ExpectedDisplayNames()))
            {
                Assert.Equal(expected, actual);
            }
        }

        private static IEnumerable<string> ExpectedDisplayNames()
        {
            yield return "gRPC - /TestService/Test";
            yield return "gRPC - Unimplemented service";
            yield return "gRPC - Unimplemented method for TestService";
        }

        [Extension("Test", "test")]
        private class TestExtension : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                throw new NotImplementedException();
            }
        }

        private class Service : TestServiceBase
        {
        }

        private class TestEndpointRouteBuilder : IEndpointRouteBuilder
        {
            public TestEndpointRouteBuilder(IServiceProvider serviceProvider)
            {
                ServiceProvider = serviceProvider;
            }

            public IServiceProvider ServiceProvider { get; }

            public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();

            public IApplicationBuilder CreateApplicationBuilder()
            {
                return new ApplicationBuilder(ServiceProvider);
            }
        }
    }
}
