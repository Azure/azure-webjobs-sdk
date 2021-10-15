// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;
using static Microsoft.Azure.WebJobs.Host.Storage.BlobServiceClientProvider;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class StorageServiceUriOptionsTests
    {
        [Fact]
        public void GetBlobServiceUri_BindsProperly()
        {
            // Value and children in the section
            var configValues = new Dictionary<string, string>
            {
                { "SomeSection:BLOBServiceUri", "https://account1.blob.core.windows.net" },
                { "SomeSection:ACCountName", "account2" },
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            var options = config.GetWebJobsConnectionSection("SomeSection").Get<StorageServiceUriOptions>();
            Assert.Equal("https://account1.blob.core.windows.net", options.BlobServiceUri);
            Assert.Equal("account2", options.AccountName);
            Assert.Equal("https://account1.blob.core.windows.net", options.GetBlobServiceUri().ToString().TrimEnd('/'));
        }

        [Fact]
        public void GetBlobServiceUri_ThrowsOnBadUri()
        {
            // Value and children in the section
            var configValues = new Dictionary<string, string>
            {
                { "SomeSection:BLOBServiceUri", "account1.blob.core.windows.net" },
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            var options = config.GetWebJobsConnectionSection("SomeSection").Get<StorageServiceUriOptions>();
            Assert.Equal("account1.blob.core.windows.net", options.BlobServiceUri);
            Assert.Throws<UriFormatException>(() => options.GetBlobServiceUri().ToString());
        }

        [Fact]
        public void GetBlobServiceUri_UsesAccountNameWithDefaults()
        {
            // Value and children in the section
            var configValues = new Dictionary<string, string>
            {
                { "SomeSection:accountName", "account1" },
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            var options = config.GetWebJobsConnectionSection("SomeSection").Get<StorageServiceUriOptions>();
            Assert.Equal("account1", options.AccountName);
            Assert.Equal("https://account1.blob.core.windows.net", options.GetBlobServiceUri().ToString().TrimEnd('/'));
        }
    }
}
