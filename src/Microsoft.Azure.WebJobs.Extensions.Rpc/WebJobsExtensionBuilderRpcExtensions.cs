// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.WebJobs.Host.Rpc.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Azure.WebJobs.Extensions.Rpc
{
    /// <summary>
    /// Extensions for registering host/worker RPC extensions on a <see cref="IWebJobsExtensionBuilder" />.
    /// </summary>
    public static class WebJobsExtensionBuilderRpcExtensions
    {
        /// <summary>
        /// Maps a gRPC service as a host/worker RPC extension.
        /// </summary>
        /// <typeparam name="T">The type of gRPC service to register.</typeparam>
        /// <param name="builder">The <see cref="IWebJobsExtensionBuilder" /> to add the gRPC service to.</param>
        /// <param name="configure">Action to configure the gRPC extension.</param>
        /// <returns>The <paramref name="builder" /> with gRPC service added.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="builder" /> is null.</exception>
        public static IWebJobsExtensionBuilder MapGrpcService<T>(
            this IWebJobsExtensionBuilder builder, Action<GrpcServiceEndpointConventionBuilder> configure = null)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(builder);
            AddCoreServices(builder.Services);
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRpcExtension>(
                new GrpcExtension<T>(builder.ExtensionInfo, configure)));
            return builder;
        }

        private static void AddCoreServices(IServiceCollection services)
        {
            services.AddGrpc();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<WebJobsRpcEndpointDataSource, ExtensionEndpointDataSource>());
        }
    }
}
