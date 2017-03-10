// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Configuration options for function result aggregation.
    /// </summary>
    public class AggregatorConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether the aggregator is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating the the maximum batch size for aggregations. When this number is hit, the results are
        /// aggregated and sent to every registered <see cref="ILogger"/>.
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets a value indicating when the aggregator will send results to every registered <see cref="ILogger"/>.
        /// </summary>
        public TimeSpan FlushTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
