// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// The storage configuration that the JobHost needs for its own operations (independent of binding)
    /// For example, this can support <see cref="SingletonAttribute"/>, blob leases, timers, etc. 
    /// This provides a common place to set storage that the various subsequent services can use. 
    /// </summary>
    public class JobHostInternalStorageOptions
    {
        public string InternalSasBlobContainer { get; set; }
    }
}