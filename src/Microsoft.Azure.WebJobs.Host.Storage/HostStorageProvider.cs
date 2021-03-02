// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs
{
    public class HostStorageProviderOptions : IConfigureOptions<HostStorageProvider>
    {
        private readonly IConfiguration _configuration;

        public HostStorageProviderOptions(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Configure(HostStorageProvider hostStorageProvider)
        {
            // TODO: Dashboard deprecated. Remove?
            if (hostStorageProvider.DashboardConnectionString == null)
            {
                hostStorageProvider.DashboardConnectionString = _configuration.GetWebJobsConnectionString(ConnectionStringNames.Dashboard);
            }

            if (hostStorageProvider.DefaultStorageConnectionString == null)
            {
                hostStorageProvider.DefaultStorageConnectionString = _configuration.GetWebJobsConnectionString(ConnectionStringNames.Storage);
            }

            hostStorageProvider.Configuration = _configuration;
        }
    }

    public class HostStorageProvider
    {
        // TODO: Dashboard deprecated. Remove?
        public string DashboardConnectionString { get; set; }
        public string DefaultStorageConnectionString { get; set; }
        public IConfiguration Configuration { get; internal set; }

        public BlobServiceClient GetBlobServiceClient(string connectionString = null)
        {
            var connectionStringToUse = connectionString ?? DefaultStorageConnectionString;
            return new BlobServiceClient(connectionStringToUse);
        }

        /// <summary>
        /// Try create BlobServiceClient
        /// SDK parse connection string method can throw exceptions: https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/storage/Azure.Storage.Common/src/Shared/StorageConnectionString.cs#L238
        /// </summary>
        /// <param name="client">client to instantiate</param>
        /// <param name="connectionString">connection string to use</param>
        /// <returns>successful client creation</returns>
        public bool TryGetBlobServiceClient(out BlobServiceClient client, string connectionString = null)
        {
            try
            {
                var connectionStringToUse = connectionString ?? DefaultStorageConnectionString;
                client = new BlobServiceClient(connectionStringToUse);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is ArgumentNullException)
            {
                client = default;
                return false;
            }
        }
    }
}
