// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Routing;

namespace Microsoft.Azure.WebJobs.Host.Grpc
{
    public interface IWebJobsGrpcExtension
    {
        void Apply(IEndpointRouteBuilder endpoints);
    }
}
