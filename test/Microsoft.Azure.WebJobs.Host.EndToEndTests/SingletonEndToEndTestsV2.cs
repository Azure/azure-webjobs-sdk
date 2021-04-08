// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Storage.Blob;
using Xunit;
using Microsoft.Extensions.Options;
using Microsoft.Azure.WebJobs.StorageProvider.Blobs;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public partial class SingletonEndToEndTestsV2 : SingletonEndToEndTests
    {
        private const string TestHostId = "e2etesthost";

        public SingletonEndToEndTestsV2() : base()
        {
        }

        [Fact]
        public override async Task SingletonFunction_StorageAccountOverride()
        {
            IHost host = CreateTestJobHost<TestJobs1>(1, (hostBuilder) =>
            {
                hostBuilder.ConfigureServices((services) =>
                {
                    services.AddSingleton<ILeaseProviderFactory, CustomLeaseProviderFactory>();
                });
            });
            await host.StartAsync();

            MethodInfo method = typeof(TestJobs1).GetMethod(nameof(TestJobs1.SingletonJob_StorageAccountOverride));

            await host.GetJobHost().CallAsync(method, new { message = "{}" });

            await host.StopAsync();
            host.Dispose();

            // make sure the lease blob was only created in the secondary account
            await VerifyLeaseDoesNotExistAsync(method, SingletonScope.Function, null);
            await VerifyLeaseState(method, SingletonScope.Function, null, LeaseState.Available, LeaseStatus.Unlocked, directory: _secondaryLockDirectory);
        }

        // Allow a host to override container resolution. 
        class CustomLeaseProviderFactory : ILeaseProviderFactory
        {
            private BlobServiceClientProvider _blobClientProvider;
            private BlobContainerClient _blobContainerClient;
            private JobHostInternalStorageOptions _options;

            public CustomLeaseProviderFactory(BlobServiceClientProvider blobClientProvider, IOptions<JobHostInternalStorageOptions> options)
            {
                _blobClientProvider = blobClientProvider;
                _options = options.Value;

                blobClientProvider.TryGet(ConnectionStringNames.Storage, out BlobServiceClient blobServiceClient);

                if (_options.InternalContainerName != null)
                {
                    _blobContainerClient = blobServiceClient.GetBlobContainerClient(_options.InternalContainerName);
                }
                else
                {
                    _blobContainerClient = blobServiceClient.GetBlobContainerClient(HostContainerNames.Hosts);
                }
            }

            public ILeaseProvider GetLeaseProvider(string lockId, string accountOverride = null)
            {
                BlobContainerClient blobContainerClient;
                if (accountOverride != null)
                {
                    _blobClientProvider.TryGet(accountOverride, out BlobServiceClient blobServiceClient);

                    if (_options.InternalContainerName != null)
                    {
                        blobContainerClient = blobServiceClient.GetBlobContainerClient(_options.InternalContainerName);
                    }
                    else
                    {
                        blobContainerClient = blobServiceClient.GetBlobContainerClient(HostContainerNames.Hosts);
                    }
                }
                else
                {
                    blobContainerClient = _blobContainerClient;
                }

                return new AzureBlobLeaseProvider(lockId, blobContainerClient);
            }
        }

        private static string FormatLockId(MethodInfo method, SingletonScope scope, string scopeId)
        {
            string lockId = string.Empty;
            if (method != null && scope == SingletonScope.Function)
            {
                lockId += string.Format("{0}.{1}", method.DeclaringType.FullName, method.Name);
            }

            if (!string.IsNullOrEmpty(scopeId))
            {
                if (!string.IsNullOrEmpty(lockId))
                {
                    lockId += ".";
                }
                lockId += scopeId;
            }

            lockId = string.Format("{0}/{1}", TestHostId, lockId);

            return lockId;
        }

        protected override IHost CreateTestJobHost<TProg>(int hostId, Action<IHostBuilder> extraConfig = null)
        {
            return base.CreateTestJobHost<TProg>(hostId, (b) =>
            {
                b.ConfigureDefaultTestHost<TProg>(webJobsBuilder =>
                {
                    RuntimeStorageWebJobsBuilderExtensions.AddAzureStorageV12CoreServices(webJobsBuilder);
                });
                extraConfig?.Invoke(b);
            });
        }
    }
}
