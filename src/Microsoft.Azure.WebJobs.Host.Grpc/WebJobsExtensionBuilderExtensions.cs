// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Azure.WebJobs.Host.Grpc
{
    public static class WebJobsExtensionBuilderExtensions
    {
        public static IWebJobsExtensionBuilder MapGrpcService<TService>(this IWebJobsExtensionBuilder builder)
            where TService : class
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IWebJobsGrpcExtension, WebJobsGrpcExtension<TService>>());

            return builder;
        }
    }
}
