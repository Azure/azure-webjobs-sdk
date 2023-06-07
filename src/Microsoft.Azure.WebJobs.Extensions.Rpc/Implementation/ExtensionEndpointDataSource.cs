// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.WebJobs.Rpc.Core.Internal;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.WebJobs.Extensions.Rpc
{
    /// <summary>
    /// EndpointDataSource for WebJobs extensions. This class is responsible for collecting endpoints
    /// registered by WebJobs extensions and then exposing them to the host/worker RPC server.
    /// </summary>
    internal sealed class ExtensionEndpointDataSource : WebJobsRpcEndpointDataSource
    {
        private readonly Lazy<List<Endpoint>> _endpoints;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtensionEndpointDataSource" /> class.
        /// </summary>
        /// <param name="services">The service provider.</param>
        /// <param name="extensions">The registered RPC extensions.</param>
        public ExtensionEndpointDataSource(
            IServiceProvider services,
            IEnumerable<IRpcExtension> extensions,
            ILogger<ExtensionEndpointDataSource> logger)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(extensions);
            ArgumentNullException.ThrowIfNull(logger);

            _endpoints = new(() =>
            {
                try
                {
                    logger.ApplyRpcExtensionsBegin();
                    ExtensionEndpointRouteBuilder builder = new(services);
                    foreach (IRpcExtension extension in extensions)
                    {
                        extension.Apply(builder, logger);
                    }

                    List<Endpoint> endpoints = builder.DataSources.SelectMany(ds => ds.Endpoints).ToList();
                    logger.ApplyRpcExtensionsEnd(endpoints.Count);
                    return endpoints;
                }
                catch (Exception ex)
                {
                    logger.ApplyRpcExtensionsError(ex);
                    throw;
                }
            });
        }

        /// <inheritdoc />
        public override IReadOnlyList<Endpoint> Endpoints => _endpoints.Value;

        /// <inheritdoc />
        public override IChangeToken GetChangeToken() => NullChangeToken.Singleton;

        private class ExtensionEndpointRouteBuilder : IEndpointRouteBuilder
        {
            public ExtensionEndpointRouteBuilder(IServiceProvider serviceProvider)
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
