// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    public interface IFunctionDataCache : IDisposable
    {
        bool TryPut(FunctionDataCacheKey cacheKey, SharedMemoryMetadata sharedMemoryMeta);

        bool TryGet(FunctionDataCacheKey cacheKey, out SharedMemoryMetadata sharedMemoryMeta);

        bool TryRemove(FunctionDataCacheKey cacheKey);
    }
}
