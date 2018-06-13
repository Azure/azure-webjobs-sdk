// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs.Host.Configuration
{
    // Need a setup class to help with binding because internal class name != Section Name. 
    internal class JobHostInternalStorageOptionsSetup : IConfigureOptions<JobHostInternalStorageOptions>
    {
        private readonly IConfiguration _config;
        public JobHostInternalStorageOptionsSetup(IConfiguration config)
        {
            _config = config;
        }

        public void Configure(JobHostInternalStorageOptions options)
        {
            _config.Bind("AzureWebJobs", options);
        }
    }
}
