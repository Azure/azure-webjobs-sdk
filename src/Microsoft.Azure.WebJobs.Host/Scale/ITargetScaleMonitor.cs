// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    public interface ITargetScaleMonitor : IScaleMonitor
    {
        Task<int> GetScaleVoteAsync(ScaleStatusContext context);
    }

    public interface ITargetScaleMonitor<TMetrics> : ITargetScaleMonitor where TMetrics : ScaleMetrics
    {
        new Task<TMetrics> GetMetricsAsync();

        Task<int> GetScaleVoteAsync(ScaleStatusContext<TMetrics> context);
    }
}
