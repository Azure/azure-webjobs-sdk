// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public static class JobHostFactory
    {
        public static TestJobHost<TProgram> Create<TProgram>()
        {
            return Create<TProgram>(CloudStorageAccount.DevelopmentStorageAccount, maxDequeueCount: 5);
        }

        public static TestJobHost<TProgram> Create<TProgram>(int maxDequeueCount)
        {
            return Create<TProgram>(CloudStorageAccount.DevelopmentStorageAccount, maxDequeueCount);
        }

        public static TestJobHost<TProgram> Create<TProgram>(CloudStorageAccount storageAccount)
        {
            return Create<TProgram>(storageAccount, maxDequeueCount: 5);
        }

        public static TestJobHost<TProgram> Create<TProgram>(CloudStorageAccount storageAccount, int maxDequeueCount)
        {
            IHostIdProvider hostIdProvider = new FixedHostIdProvider("test");
            JobHostOptions config = TestHelpers.NewConfig<TProgram>(hostIdProvider);

            var s = new ServiceCollection().BuildServiceProvider();
            IStorageAccountProvider storageAccountProvider = new SimpleStorageAccountProvider(s)
            {
                StorageAccount = storageAccount,
                // use null logging string since unit tests don't need logs.
                DashboardAccount = null
            };
            // TODO: DI: This needs to be updated to perform proper service registration
            // config.AddServices(storageAccountProvider);
            return new TestJobHost<TProgram>(new OptionsWrapper<JobHostOptions>(new JobHostOptions()), null);
        }
    }
}
