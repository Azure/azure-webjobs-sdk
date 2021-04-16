// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// TODO
    /// </summary>
    public interface ICacheAfterWriteStream
    {
        Task<bool> TryPutToFunctionDataCacheAsync();
    }
}
