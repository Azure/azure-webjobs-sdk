// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    public interface IFunctionDataCache : IDisposable
    {
        bool IsEnabled { get; }

        bool TryPut(FunctionDataCacheKey cacheKey, SharedMemoryMetadata sharedMemoryMeta, bool isIncrementActiveReference);

        bool TryGet(FunctionDataCacheKey cacheKey, bool isIncrementActiveReference, out SharedMemoryMetadata sharedMemoryMeta);

        bool TryRemove(FunctionDataCacheKey cacheKey);

        void DecrementActiveReference(FunctionDataCacheKey cacheKey);
    }
}
