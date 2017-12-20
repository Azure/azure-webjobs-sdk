// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// The storage configuration that the JobHost needs for its own operations (independent of binding)
    /// For example, this can support <see cref="SingletonAttribute"/>, blob leases, timers, etc. 
    /// This provides a common place to set storage that the various subsequent services can use. 
    /// </summary>
    public class JobHostInternalStorageConfiguration
    {
        /// <summary>
        /// A SAS to a Blob Container. This allows services to create blob leases and do distributed locking.
        /// If this is set, <see cref="JobHostConfiguration.StorageConnectionString"/> and 
        /// <see cref="JobHostConfiguration.DashboardConnectionString"/> can be set to null and the runtime will use the container.
        /// </summary>
        public CloudBlobContainer InternalContainer { get; set; }
    }
}