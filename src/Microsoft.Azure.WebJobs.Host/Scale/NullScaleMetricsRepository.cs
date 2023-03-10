// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    internal class NullScaleMetricsRepository : IScaleMetricsRepository
    {
        private IDictionary<IScaleMonitor, IList<ScaleMetrics>> _emptyMetrics = new Dictionary<IScaleMonitor, IList<ScaleMetrics>>();

        public Task WriteMetricsAsync(IDictionary<IScaleMonitor, ScaleMetrics> monitorMetrics)
        {
            return Task.CompletedTask;
        }

        public Task<IDictionary<IScaleMonitor, IList<ScaleMetrics>>> ReadMetricsAsync(IEnumerable<IScaleMonitor> monitors)
        {
            return Task.FromResult((IDictionary<IScaleMonitor, IList<ScaleMetrics>>)_emptyMetrics);
        }
    }
}
