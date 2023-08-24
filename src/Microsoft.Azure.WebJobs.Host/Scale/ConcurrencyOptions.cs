// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Options used to configure dynamic concurrency control.
    /// </summary>
    public class ConcurrencyOptions : IOptionsFormatter
    {
        private int _maximumFunctionConcurrency;
        private long _totalAvaliableMemoryBytes;
        private float _memoryThreshold;
        private float _cpuThreshold;

        public ConcurrencyOptions()
        {
            SnapshotPersistenceEnabled = true;
            _maximumFunctionConcurrency = 500;
            _totalAvaliableMemoryBytes = -1;
            _cpuThreshold = 0.80F;
            _memoryThreshold = 0.80F;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the dynamic concurrency control feature
        /// is enabled.
        /// </summary>
        public bool DynamicConcurrencyEnabled { get; set; }

        /// <summary>
        /// Gets or sets the maximum concurrency that will be enforced per function.
        /// Set to -1 to indicate no limit.
        /// </summary>
        public int MaximumFunctionConcurrency 
        { 
            get
            {
                return _maximumFunctionConcurrency;
            }
            set
            {
                if (value <= 0 && value != -1)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaximumFunctionConcurrency));
                }

                _maximumFunctionConcurrency = value;
            }
        }

        /// <summary>
        /// Gets or sets the total amount of physical memory the host has access to.
        /// This value is used in conjunction with <see cref="MemoryThreshold"/> to
        /// determine when memory based throttling will kick in.
        /// A value of -1 indicates that the available memory limit is unknown, and
        /// memory based throtting will be disabled.
        /// </summary>
        /// <remarks>
        /// When deployed to App Service, this value will be defaulted based on the SKU
        /// and other plan info.
        /// </remarks>
        internal long TotalAvailableMemoryBytes
        { 
            get
            {
                return _totalAvaliableMemoryBytes;
            }
            set
            {
                if (value <= 0 && value != -1)
                {
                    throw new ArgumentOutOfRangeException(nameof(TotalAvailableMemoryBytes));
                }

                _totalAvaliableMemoryBytes = value;
            }
        }

        /// <summary>
        /// Gets or sets the memory threshold dictating when memory based throttling
        /// will kick in. The value should be between 0 and 1 (exclusive) indicating a percentage
        /// of <see cref="TotalAvailableMemoryBytes"/>.
        /// Set to -1 to disable memory based throttling.
        /// </summary>
        internal float MemoryThreshold 
        {
            get
            {
                return _memoryThreshold;
            }
            set
            {
                if ((value <= 0 && value != -1) || (value >= 1))
                {
                    throw new ArgumentOutOfRangeException(nameof(MemoryThreshold));
                }

                _memoryThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the CPU threshold dictating when cpu based throttling
        /// will kick in. Value should be between 0 and 1 indicating a cpu percentage.
        /// </summary>
        public float CPUThreshold
        {
            get
            {
                return _cpuThreshold;
            }
            set
            {
                if (value <= 0 || value >= 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(CPUThreshold));
                }

                _cpuThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether concurrency status snapshots will be periodically
        /// written to persistent storage, to enable hosts to remember and apply previously learned levels
        /// on startup.
        /// </summary>
        public bool SnapshotPersistenceEnabled { get; set; }

        internal bool MemoryThrottleEnabled
        {
            get
            {
                return TotalAvailableMemoryBytes > 0 && MemoryThreshold > 0;
            }
        }

        public string Format()
        {
            JObject options = new JObject
            {
                { nameof(DynamicConcurrencyEnabled), DynamicConcurrencyEnabled },
                { nameof(MaximumFunctionConcurrency), MaximumFunctionConcurrency },
                // TODO: Once Memory monitoring is public add this back
                // https://github.com/Azure/azure-webjobs-sdk/issues/2733
                //{ nameof(TotalAvailableMemoryBytes), TotalAvailableMemoryBytes },
                //{ nameof(MemoryThreshold), MemoryThreshold },
                { nameof(CPUThreshold), CPUThreshold },
                { nameof(SnapshotPersistenceEnabled), SnapshotPersistenceEnabled }
            };

            return options.ToString(Formatting.Indented);
        }
    }
}
