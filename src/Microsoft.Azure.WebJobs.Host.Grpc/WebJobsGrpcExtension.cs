// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Azure.WebJobs.Host.Grpc
{
    internal class WebJobsGrpcExtension<T> : IWebJobsGrpcExtension
        where T : class
    {
        public void Apply(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<T>();
        }
    }
}
