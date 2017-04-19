// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Lease;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Lease
{
    public class BlobLeaseProxyTests
    {
        const string Namespace1 = "Namespace1";
        const string Namespace2 = "Namespace2";
        const string Namespace1LeaseName = "Namespace1LeaseName";
        const string Namespace2LeaseName = "Namespace2LeaseName";

        [Fact]
        public void GetBlob_DifferentNestedNamespaces()
        {
            var leaseDefinition = new LeaseDefinition
            {
                AccountName = "someaccount",
            };

            var accountProvider = GetMockInstanceOfType<IStorageAccountProvider>();
            var account = GetMockInstanceOfType<IStorageAccount>();
            var blobClient = GetMockInstanceOfType<IStorageBlobClient>();
            var container = GetMockInstanceOfType<IStorageBlobContainer>();
            var directory = GetMockInstanceOfType<IStorageBlobDirectory>();
            var containerBlob = GetMockInstanceOfType<IStorageBlockBlob>();
            var directoryBlob = GetMockInstanceOfType<IStorageBlockBlob>();

            accountProvider
                .Setup(p => p.TryGetAccountAsync(leaseDefinition.AccountName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account.Object);

            // Creates Mock objects to represent a lease structure like the following:
            // 
            // Container (Namespace1) -> Directory (Namespace2) -> DirectoryBlob (Namespace2LeaseName)
            //    |
            //    +-------> ContainerBlob (Namespace1LeaseName)
            //

            account
                .SetupGet(a => a.Type)
                .Returns(StorageAccountType.GeneralPurpose);

            account
                .Setup(p => p.CreateBlobClient(null))
                .Returns(blobClient.Object);

            blobClient
                .Setup(p => p.GetContainerReference(Namespace1))
                .Returns(container.Object);

            container
                .Setup(p => p.GetDirectoryReference(Namespace2))
                .Returns(directory.Object);

            container
                .Setup(p => p.GetBlockBlobReference(Namespace1LeaseName))
                .Returns(containerBlob.Object);

            directory
                .Setup(p => p.GetBlockBlobReference(Namespace2LeaseName))
                .Returns(directoryBlob.Object);

            leaseDefinition.Namespaces = new List<string> { Namespace1 };
            leaseDefinition.Name = Namespace1LeaseName;
            var blobLeaseProxy = new BlobLeaseProxy(accountProvider.Object);
            var blob = blobLeaseProxy.GetBlob(leaseDefinition);
            Assert.Same(containerBlob.Object, blob);

            leaseDefinition.Namespaces = new List<string> { Namespace1, Namespace2 };
            leaseDefinition.Name = Namespace2LeaseName;
            blobLeaseProxy = new BlobLeaseProxy(accountProvider.Object);
            blob = blobLeaseProxy.GetBlob(leaseDefinition);
            Assert.Same(directoryBlob.Object, blob);
        }

        [Fact]
        public void GetBlob_MultipleAccounts()
        {
            var leaseDefinition = new LeaseDefinition
            {
                Namespaces = new List<string> { Namespace1 },
                Name = Namespace1LeaseName
            };

            var accountProvider = GetMockInstanceOfType<IStorageAccountProvider>();

            var blobLeaseProxy = new BlobLeaseProxy(accountProvider.Object);

            leaseDefinition.AccountName = ConnectionStringNames.Lease;
            var containerBlob = SetupMocks(accountProvider, leaseDefinition.AccountName);
            var blob = blobLeaseProxy.GetBlob(leaseDefinition);
            Assert.Same(containerBlob.Object, blob);

            leaseDefinition.AccountName = ConnectionStringNames.Storage;
            containerBlob = SetupMocks(accountProvider, leaseDefinition.AccountName);
            blob = blobLeaseProxy.GetBlob(leaseDefinition);
            Assert.Same(containerBlob.Object, blob);

            leaseDefinition.AccountName = "some-account-name";
            containerBlob = SetupMocks(accountProvider, leaseDefinition.AccountName);
            blob = blobLeaseProxy.GetBlob(leaseDefinition);
            Assert.Same(containerBlob.Object, blob);
        }

        // Creates Mock objects to represent a lease structure like the following:
        // 
        // Container (Namespace1) -> ContainerBlob (Namespace1LeaseName)
        private static Mock<IStorageBlockBlob> SetupMocks(Mock<IStorageAccountProvider> accountProvider, string accountName)
        {
            var account = GetMockInstanceOfType<IStorageAccount>();
            var blobClient = GetMockInstanceOfType<IStorageBlobClient>();
            var container = GetMockInstanceOfType<IStorageBlobContainer>();
            var containerBlob = GetMockInstanceOfType<IStorageBlockBlob>();

            accountProvider
                .Setup(p => p.TryGetAccountAsync(accountName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account.Object);

            account
                .SetupGet(a => a.Type)
                .Returns(StorageAccountType.GeneralPurpose);

            account
                .Setup(p => p.CreateBlobClient(null))
                .Returns(blobClient.Object);

            blobClient
                .Setup(p => p.GetContainerReference(Namespace1))
                .Returns(container.Object);

            container
                .Setup(p => p.GetBlockBlobReference(Namespace1LeaseName))
                .Returns(containerBlob.Object);

            return containerBlob;
        }

        private static Mock<T> GetMockInstanceOfType<T>() where T: class
        {
            return new Mock<T>(MockBehavior.Strict);
        }
    }
}
