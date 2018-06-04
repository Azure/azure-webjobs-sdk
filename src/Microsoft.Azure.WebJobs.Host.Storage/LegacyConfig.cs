// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs
{

    // $$$ Validate these?  And what are their capabilities? 
    public class LegacyConfigSetup : IConfigureOptions<LegacyConfig>
    {
        private readonly IConnectionStringProvider _provider;

        public LegacyConfigSetup(IConnectionStringProvider provider)
        {
            _provider = provider;
        }

        public void Configure(LegacyConfig options)
        {
            if (options.Dashboard == null)
            {
                options.Dashboard = _provider.GetConnectionString("Dashboard");
            }
            if (options.Storage == null)
            {
                options.Storage = _provider.GetConnectionString("Storage");
            }
        }
    }

    // From config.
    // Like JobHostInternalStorageOptionsSetup
    public class LegacyConfig
    {
        // Property names here must match existing names. 
        public string Dashboard { get; set; }
        public string Storage { get; set; }
        public string InternalSasBlobContainer { get; set; }

        public CloudStorageAccount GetDashboardStorageAccount()
        {
            CloudStorageAccount account;
            CloudStorageAccount.TryParse(this.Dashboard, out account);
            return account;
        }

        public CloudStorageAccount GetStorageAccount()
        {
            CloudStorageAccount account;
            CloudStorageAccount.TryParse(this.Storage, out account);
            return account;
        }
    }
}
