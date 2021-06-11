// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class ProcessMonitorTests
    {
        private readonly ProcessMonitor _monitor;
        private List<TimeSpan> _testProcessorTimeValues;
        private List<long> _testPrivateMemoryValues;

        public ProcessMonitorTests()
        {
            _testProcessorTimeValues = new List<TimeSpan>();
            _testPrivateMemoryValues = new List<long>();
            int effectiveCores = 1;
            _monitor = new ProcessMonitor(Process.GetCurrentProcess(), new TestProcessMetricsProvider(_testProcessorTimeValues, _testPrivateMemoryValues), effectiveCores: effectiveCores, autoStart: false);
        }

        public static IEnumerable<object[]> CPULoadTestData =>
            new List<object[]>
            {
                new object[]
                {
                    new List<double> { 0, 250, 750, 1500, 2500, 3300, 3900, 4600, 5100, 5500, 5800 },
                    new List<double> { 25, 50, 75, 100, 80, 60, 70, 50, 40, 30 }
                },
                new object[]
                {
                    new List<double> { 100, 300, 500, 700, 1000, 1500, 2000, 3000, 3850, 4800, 5600, 6300, 7000, 7500, 7800, 8000 },
                    new List<double> { 50, 100, 85, 95, 80, 70, 70, 50, 30, 20 }
                }
            };

        public static IEnumerable<object[]> MemoryUsageTestData =>
            new List<object[]>
            {
                new object[]
                {
                    new List<long> { 524288000, 524690234, 525598012, 528710023, 538765843, 560987645, 598712101, 628928343, 645986754, 645985532, 656887110, 667853423 }
                }
            };

        [Fact]
        public async Task GetStats_StartsSampleTimer()
        {
            int intervalMS = 10;
            var localMonitor = new ProcessMonitor(Process.GetCurrentProcess(), TimeSpan.FromMilliseconds(intervalMS));

            var stats = localMonitor.GetStats();
            Assert.Empty(stats.CpuLoadHistory);
            Assert.Empty(stats.MemoryUsageHistory);

            localMonitor.GetStats();

            // wait long enough for enough samples to be taken to verify sample history rolling
            await Task.Delay(2 * ProcessMonitor.SampleHistorySize * intervalMS);

            stats = localMonitor.GetStats();
            Assert.Equal(ProcessMonitor.SampleHistorySize, stats.CpuLoadHistory.Count());
            Assert.Equal(ProcessMonitor.SampleHistorySize, stats.MemoryUsageHistory.Count());
        }

        [Theory]
        [MemberData(nameof(CPULoadTestData))]
        public void SampleCPULoad_AccumulatesSamples(List<double> testProcessorTimeValues, List<double> expectedLoadValues)
        {
            _testProcessorTimeValues.AddRange(testProcessorTimeValues.Select(p => TimeSpan.FromMilliseconds(p)));

            var stats = _monitor.GetStats();
            Assert.Empty(stats.CpuLoadHistory);

            // start taking samples, using a constant duration so our expected
            // calculations are deterministic
            var sampleDuration = TimeSpan.FromSeconds(1);
            for (int i = 0; i < _testProcessorTimeValues.Count; i++)
            {
                _monitor.SampleCPULoad(sampleDuration);
            }

            stats = _monitor.GetStats();

            // expect a max of 10 - old samples are removed
            var cpuLoadResults = stats.CpuLoadHistory.ToList();
            Assert.Equal(Math.Min(cpuLoadResults.Count, ProcessMonitor.SampleHistorySize), cpuLoadResults.Count);

            Assert.Equal(expectedLoadValues, cpuLoadResults);
        }

        [Theory]
        [MemberData(nameof(MemoryUsageTestData))]
        public void SampleMemoryUsage_AccumulatesSamples(List<long> testMemoryValues)
        {
            _testPrivateMemoryValues.AddRange(testMemoryValues);

            var stats = _monitor.GetStats();
            Assert.Empty(stats.MemoryUsageHistory);

            // start taking samples
            for (int i = 0; i < _testPrivateMemoryValues.Count; i++)
            {
                _monitor.SampleMemoryUsage();
            }

            stats = _monitor.GetStats();

            // expect a max of 10 - old samples are removed
            var memoryUsageResults = stats.MemoryUsageHistory.ToList();
            Assert.Equal(Math.Min(memoryUsageResults.Count, ProcessMonitor.SampleHistorySize), memoryUsageResults.Count);

            int skip = testMemoryValues.Count - 10;
            var expectedMemoryValues = testMemoryValues.Skip(skip).ToList();
            Assert.Equal(expectedMemoryValues, memoryUsageResults);
        }

        [Theory]
        [InlineData(1000, 3500, 3000, 1, 50)]
        [InlineData(1000, 3400, 3000, 4, 10)]
        [InlineData(1000, 3500, 3250, 1, 25)]
        [InlineData(1000, 2500, 1000, 1, 100)]
        public void CalculateCpuLoad(int sampleDurationMS, int currProcessorTimeMS, int lastProcessorTimeMS, int coreCount, double expected)
        {
            double cpuLoad = ProcessMonitor.CalculateCpuLoad(TimeSpan.FromMilliseconds(sampleDurationMS), TimeSpan.FromMilliseconds(currProcessorTimeMS), TimeSpan.FromMilliseconds(lastProcessorTimeMS), coreCount);
            Assert.Equal(expected, cpuLoad);
        }

        private class TestProcessMetricsProvider : IProcessMetricsProvider
        {
            private int _idx = 0;
            private List<TimeSpan> _processorTimeValues;
            private List<long> _privateMemoryValues;

            public TestProcessMetricsProvider(List<TimeSpan> processorTimeValues, List<long> privateMemoryValues)
            {
                _processorTimeValues = processorTimeValues;
                _privateMemoryValues = privateMemoryValues;
            }

            public TimeSpan TotalProcessorTime
            {
                get
                {
                    return _processorTimeValues[_idx++];
                }
            }

            public long PrivateMemoryBytes
            {
                get
                {
                    return _privateMemoryValues[_idx++];
                }
            }
        }
    }
}
