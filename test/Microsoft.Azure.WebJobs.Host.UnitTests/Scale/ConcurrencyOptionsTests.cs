// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Text.RegularExpressions;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class ConcurrencyOptionsTests
    {
        [Fact]
        public void Constructor_Defaults()
        {
            var options = new ConcurrencyOptions();

            Assert.Equal(true, options.SnapshotPersistenceEnabled);
            Assert.Equal(500, options.MaximumFunctionConcurrency);
            Assert.Equal(-1, options.TotalAvailableMemoryBytes);
            Assert.Equal(0.80F, options.CPUThreshold);
            Assert.Equal(0.80F, options.MemoryThreshold);

            Assert.False(options.MemoryThrottleEnabled);
        }

        [Theory]
        [InlineData(100000000, 0.8F, true)]
        [InlineData(100000000, -1, false)]
        [InlineData(-1, 0.8F, false)]
        public void MemoryThrottleEnabled_ReturnsExpectedValue(int availableMemoryBytes, float memoryThreshold, bool expected)
        {
            var options = new ConcurrencyOptions
            {
                TotalAvailableMemoryBytes = availableMemoryBytes,
                MemoryThreshold = memoryThreshold
            };

            Assert.Equal(expected, options.MemoryThrottleEnabled);
        }

        [Fact]
        public void MaximumFunctionConcurrency_Validation()
        {
            var options = new ConcurrencyOptions();

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.MaximumFunctionConcurrency = 0);
            Assert.Equal(nameof(ConcurrencyOptions.MaximumFunctionConcurrency), ex.ParamName);

            options.MaximumFunctionConcurrency = -1;
            Assert.Equal(-1, options.MaximumFunctionConcurrency);

            options.MaximumFunctionConcurrency = 100;
            Assert.Equal(100, options.MaximumFunctionConcurrency);
        }

        [Fact]
        public void TotalAvailableMemoryBytes_Validation()
        {
            var options = new ConcurrencyOptions();

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.TotalAvailableMemoryBytes = 0);
            Assert.Equal(nameof(ConcurrencyOptions.TotalAvailableMemoryBytes), ex.ParamName);

            options.TotalAvailableMemoryBytes = -1;
            Assert.Equal(-1, options.TotalAvailableMemoryBytes);

            options.TotalAvailableMemoryBytes = 3000000000;
            Assert.Equal(3000000000, options.TotalAvailableMemoryBytes);
        }

        [Fact]
        public void MemoryThreshold_Validation()
        {
            var options = new ConcurrencyOptions();

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.MemoryThreshold = 0);
            Assert.Equal(nameof(ConcurrencyOptions.MemoryThreshold), ex.ParamName);

            ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.MemoryThreshold = 1);
            Assert.Equal(nameof(ConcurrencyOptions.MemoryThreshold), ex.ParamName);

            ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.MemoryThreshold = 1.1F);
            Assert.Equal(nameof(ConcurrencyOptions.MemoryThreshold), ex.ParamName);

            options.MemoryThreshold = -1;
            Assert.Equal(-1, options.MemoryThreshold);

            options.MemoryThreshold = 0.75F;
            Assert.Equal(0.75, options.MemoryThreshold);
        }

        [Fact]
        public void CPUThreshold_Validation()
        {
            var options = new ConcurrencyOptions();

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.CPUThreshold = 0);
            Assert.Equal(nameof(ConcurrencyOptions.CPUThreshold), ex.ParamName);

            ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.CPUThreshold = 1);
            Assert.Equal(nameof(ConcurrencyOptions.CPUThreshold), ex.ParamName);

            ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.CPUThreshold = 1.1F);
            Assert.Equal(nameof(ConcurrencyOptions.CPUThreshold), ex.ParamName);

            options.CPUThreshold = 0.75F;
            Assert.Equal(0.75, options.CPUThreshold);
        }

        [Fact]
        public void Format_ReturnsExpectedResult()
        {
            var options = new ConcurrencyOptions
            {
                DynamicConcurrencyEnabled = true,
                TotalAvailableMemoryBytes = 3000000,
                CPUThreshold = 0.85F,
                MemoryThreshold = 0.85F,
                SnapshotPersistenceEnabled = true,
                MaximumFunctionConcurrency = 1000
            };

            string result = options.Format();
            string expected = @"{
  ""DynamicConcurrencyEnabled"": true,
  ""MaximumFunctionConcurrency"": 1000,
  ""CPUThreshold"": 0.85,
  ""SnapshotPersistenceEnabled"": true
}";
            Assert.Equal(Regex.Replace(expected, @"\s+", ""), Regex.Replace(result, @"\s+", ""));
        }
    }
}
