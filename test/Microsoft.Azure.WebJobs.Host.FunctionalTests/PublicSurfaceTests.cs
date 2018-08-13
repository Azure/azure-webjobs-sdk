// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    /// <summary>
    /// These tests help maintain our public surface area + dependencies. They will
    /// fail any time new dependencies or public surface area are added, ensuring
    /// we review such additions carefully.
    /// </summary>
    public class PublicSurfaceTests
    {
        [Fact]
        public void WebJobs_Host_Storage_VerifyPublicSurfaceArea()
        {
            var assembly = typeof(RuntimeStorageWebJobsBuilderExtensions).Assembly;

            var expected = new[]
            {
                "CloudBlobContainerDistributedLockManager",
                "DistributedLockManagerContainerProvider",
                "JobHostInternalStorageOptions",
                "LegacyConfig",
                "LegacyConfigSetup",
                "RuntimeStorageWebJobsBuilderExtensions",
                "StorageBaseDistributedLockManager",
                "StorageServiceCollectionExtensions"
            };

            TestHelpers.AssertPublicTypes(expected, assembly);
        }
    }
}
