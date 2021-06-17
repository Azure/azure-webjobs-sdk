// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class DefaultProcessMetricsProviderTests
    {
        [Fact]  
        public void TotalProcessorTime_ReturnsExpectedResult()
        {
            var process = Process.GetCurrentProcess();
            var provider = new DefaultProcessMetricsProvider(process);

            Assert.Equal(process.TotalProcessorTime, provider.TotalProcessorTime);
            Assert.Equal(process.TotalProcessorTime, provider.TotalProcessorTime);

            process.Refresh();

            Assert.Equal(process.TotalProcessorTime, provider.TotalProcessorTime);
        }

        [Fact]
        public void PrivateMemoryBytes_ReturnsExpectedResult()
        {
            var process = Process.GetCurrentProcess();
            var provider = new DefaultProcessMetricsProvider(process);

            Assert.Equal(process.PrivateMemorySize64, provider.PrivateMemoryBytes);
            Assert.Equal(process.PrivateMemorySize64, provider.PrivateMemoryBytes);

            process.Refresh();

            Assert.Equal(process.PrivateMemorySize64, provider.PrivateMemoryBytes);
        }
    }
}
