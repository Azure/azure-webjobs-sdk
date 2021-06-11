// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// This type was moved from https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script/Scale/DefaultProcessMetricsProvider.cs


using System;
using System.Diagnostics;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Default implementation, that just delegates to the underlying Process.
    /// </summary>
    internal class DefaultProcessMetricsProvider : IProcessMetricsProvider
    {
        private readonly Process _process;

        public DefaultProcessMetricsProvider(Process process)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
        }

        public TimeSpan TotalProcessorTime
        {
            get
            {
                return _process.TotalProcessorTime;
            }
        }

        public long PrivateMemoryBytes
        {
            get
            {
                return _process.PrivateMemorySize64;
            }
        }
    }
}
