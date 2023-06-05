// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Rpc
{
    /// <summary>
    /// A gRPC-based host/worker RPC extension.
    /// </summary>
    /// <typeparam name="TService">The type of gRPC service to register to handle RPC communication.</typeparam>
    internal sealed class GrpcExtension<TService> : IRpcExtension
        where TService : class
    {
        private readonly ExtensionInfo _extension;

        /// <summary>
        /// Initializes a new instance of the <see cref="GrpcExtension{TService}" /> class.
        /// </summary>
        public GrpcExtension(ExtensionInfo extension)
        {
            _extension = extension;
        }

        /// <inheritdoc />
        public void Apply(IEndpointRouteBuilder builder, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(builder);

            logger?.GrpcServiceApplied(_extension.Name, typeof(TService).Name);
            builder.MapGrpcService<TService>();
        }
    }
}
