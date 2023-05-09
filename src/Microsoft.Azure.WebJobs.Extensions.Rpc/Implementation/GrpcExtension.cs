// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Rpc
{
    /// <summary>
    /// A gRPC-based host/worker RPC extension.
    /// </summary>
    /// <typeparam name="TService">The type of gRPC service to register to handle RPC communication.</typeparam>
    internal sealed class GrpcExtension<TService> : IRpcExtension
        where TService : class
    {
        private readonly ExtensionInfo _extension;
        private readonly Action<GrpcServiceEndpointConventionBuilder> _configure;

        /// <summary>
        /// Initializes a new instance of the <see cref="GrpcExtension{TService}" /> class.
        /// </summary>
        /// <param name="configure"></param>
        public GrpcExtension(ExtensionInfo extension, Action<GrpcServiceEndpointConventionBuilder> configure)
        {
            _extension = extension;
            _configure = configure;
        }

        /// <inheritdoc />
        public void Apply(IEndpointRouteBuilder builder, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(builder);

            logger?.GrpcServiceApplied(_extension.Name, typeof(TService).Name);
            GrpcServiceEndpointConventionBuilder b = builder.MapGrpcService<TService>();
            _configure?.Invoke(b);
        }
    }
}
