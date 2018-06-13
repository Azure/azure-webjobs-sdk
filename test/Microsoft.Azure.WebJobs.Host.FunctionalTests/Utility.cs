// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.FunctionalTests.TestDoubles;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    static class Utility
    {
        public static IHostBuilder ConfigureDefaultTestHost<TProgram>(this IHostBuilder builder, XStorageAccount account)
        {
            return builder.ConfigureDefaultTestHost<TProgram>()
                .ConfigureServices(services => services.AddFakeStorageAccountProvider(account));
        }

        public static IHostBuilder ConfigureFakeStorageAccount(this IHostBuilder builder)
        {
            return builder.ConfigureServices(services =>
            {
                services.AddFakeStorageAccountProvider();
            });
        }

        public static IServiceCollection AddFakeStorageAccountProvider(this IServiceCollection services)
        {
            // return services.AddFakeStorageAccountProvider(new XFakeStorageAccount()); $$$
            throw new NotImplementedException();
        }

        public static IServiceCollection AddNullLoggerProviders(this IServiceCollection services)
        {
             return services
                .AddSingleton<IFunctionOutputLoggerProvider, NullFunctionOutputLoggerProvider>()
                .AddSingleton<IFunctionInstanceLoggerProvider, NullFunctionInstanceLoggerProvider>();
        }

        public static IServiceCollection AddFakeStorageAccountProvider(this IServiceCollection services, XStorageAccount account)
        {
            throw new NotImplementedException();
            /*
            if (account is XFakeStorageAccount)
            {
                services.AddNullLoggerProviders();
            }

            return services.AddSingleton<XStorageAccountProvider>(new FakeStorageAccountProvider(account));
            */
        }
    }
}
