// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.FunctionalTests.TestDoubles;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    static class Utility
    {
        // Helper for quickly testing indexing errors       
        public static void AssertIndexingError<TProgram>(string methodName, string expectedErrorMessage)
        {
            // Need to pass an account to get passed initial validation checks. 
            IStorageAccount account = new FakeStorageAccount();

            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<TProgram>()
                .ConfigureServices(services => services.AddFakeStorageAccountProvider())
                .Build();

            host.GetJobHost<TProgram>().AssertIndexingError(methodName, expectedErrorMessage);
        }

        public static IHostBuilder ConfigureDefaultTestHost<TProgram>(this IHostBuilder builder, IStorageAccount account)
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
            return services.AddFakeStorageAccountProvider(new FakeStorageAccount());
        }

        public static IServiceCollection AddNullLoggerProviders(this IServiceCollection services)
        {
             return services
                .AddSingleton<IFunctionOutputLoggerProvider, NullFunctionOutputLoggerProvider>()
                .AddSingleton<IFunctionInstanceLoggerProvider, NullFunctionInstanceLoggerProvider>();
        }

        public static IServiceCollection AddFakeStorageAccountProvider(this IServiceCollection services, IStorageAccount account)
        {
            if (account is FakeStorageAccount)
            {
                services.AddNullLoggerProviders();
            }

            return services.AddSingleton<IStorageAccountProvider>(new FakeStorageAccountProvider
            {
                StorageAccount = account,
                DashboardAccount = account
            });
        }
    }
}
