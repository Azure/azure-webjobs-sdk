// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    public interface IDynamicTargetValueProvider
    {
        Task<int> GetDynamicTargetValueAsync(string functionId);
    }
}
