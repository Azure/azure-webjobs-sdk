// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Castle.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.Rpc.Implementation.UnitTests
{
    public class ExtensionEndpointDataSourceTests
    {
        private static readonly ILogger<ExtensionEndpointDataSource> Logger
            = NullLogger<ExtensionEndpointDataSource>.Instance;

        private readonly IServiceProvider _serviceProvider;

        public ExtensionEndpointDataSourceTests()
        {
            ServiceCollection services = new();
            services.AddLogging().AddRouting();
            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public void Endpoints_AppliesExtensions()
        {
            TestRpcExtension[] extensions = new[]
            {
                new TestRpcExtension(b => b.MapGet("first/endpoint", d => Task.CompletedTask)),
                new TestRpcExtension(b => b.MapPost("second/endpoint", d => Task.CompletedTask)),
            };

            ExtensionEndpointDataSource source = new(_serviceProvider, extensions, Logger);

            IReadOnlyList<Endpoint> endpoints = source.Endpoints;
            Assert.Equal(2, endpoints.Count);
            Assert.Equal("first/endpoint HTTP: GET", endpoints[0].DisplayName);
            Assert.Equal("second/endpoint HTTP: POST", endpoints[1].DisplayName);

            foreach (TestRpcExtension ext in extensions)
            {
                Assert.Equal(_serviceProvider, ext.Builder.ServiceProvider);
            }
        }

        private class TestRpcExtension : IRpcExtension
        {
            private readonly Action<IEndpointRouteBuilder> _configure;

            public TestRpcExtension(Action<IEndpointRouteBuilder> configure)
            {
                _configure = configure;
            }

            public IEndpointRouteBuilder Builder { get; private set; }

            public void Apply(IEndpointRouteBuilder builder, ILogger logger)
            {
                Assert.NotNull(logger);
                Builder = builder;
                _configure.Invoke(builder);
            }
        }
    }
}
