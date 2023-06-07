// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Rpc.Core.Internal;
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
        /// <returns>The <paramref name="builder" /> with gRPC service added.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="builder" /> is null.</exception>
        /// <remarks>
        /// This gRPC service is <b>not</b> for external gRPC communication. It is only for RPC between the
        /// out-of-proc worker and the host.
        /// </remarks>
        public static IWebJobsExtensionBuilder MapWorkerGrpcService<T>(this IWebJobsExtensionBuilder builder)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(builder);
            AddCoreServices(builder.Services);
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRpcExtension>(
                new GrpcExtension<T>(builder.ExtensionInfo)));
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
