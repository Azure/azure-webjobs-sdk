// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Rpc
{
    /// <summary>
    /// Represents an extension for host/worker RPC communication.
    /// </summary>
    internal interface IRpcExtension
    {
        /// <summary>
        /// Applies the RPC extension to the <see cref="IEndpointRouteBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="IEndpointRouteBuilder" /> to apply to.</param>
        /// <param name="logger">The logger to use during extension registration.</param>
        void Apply(IEndpointRouteBuilder builder, ILogger logger);
    }
}
