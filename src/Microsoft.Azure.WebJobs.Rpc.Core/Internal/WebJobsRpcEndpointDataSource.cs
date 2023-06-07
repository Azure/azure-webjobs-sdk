// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Azure.WebJobs.Rpc.Core.Internal
{
    /// <summary>
    /// This is an internal API that supports the WebJobs infrastructure and not subject to the same compatibility
    /// standards as public APIs. It may be changed or removed without notice in any release. You should only use it
    /// directly in your code with extreme caution and knowing that doing so can result in application failures when
    /// updating to a new WebJobs release.
    /// </summary>
    [Obsolete("Not for public consumption.")]
    public abstract class WebJobsRpcEndpointDataSource : EndpointDataSource
    {
        /*
            This contract, and entire assembly, is meant to be a common reference point between the WebJobs host
            code and WebJobs extensions. We expose a derived EndpointDataSource because it already has the shape
            we need, but we derive a new type to let us retrieve endpoints meant explicitly for us from the
            IServiceProvider, without accidentally import other EndpointDataSources.

            EndpointDataSource was also chosen as the extension point due to how assembly load contexts work with
            WebJob extensions. Being in an isolated load context, we need an extension point that relies only on
            types which are in the unified assembly load context. We cannot rely on something like Grpc.AspNetCore
            as that is not unified, and types will not work between services declared in WebJob extensions and our
            own RPC server running in the host. This leaves us with the rather broad extension point we have here
            as AspNetCore assemblies (but not Grpc ones) are unified.
        */
    }
}
