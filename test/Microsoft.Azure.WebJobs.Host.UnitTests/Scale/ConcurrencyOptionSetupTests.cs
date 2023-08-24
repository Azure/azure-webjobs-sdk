// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class ConcurrencyOptionSetupTests
    {
        [Theory]
        [InlineData("standard", 4, 7516192768)]
        [InlineData("dynamic", 1, 1610612736)]
        [InlineData(null, 4, -1)]
        [InlineData("", 4, -1)]
        [InlineData("invalidsku", 4, -1)]
        public void Configure_Memory_SetsExpectedValues(string sku, int numCores, long expectedMemory)
        {
            var options = new ConcurrencyOptions();
            ConcurrencyOptionsSetup.ConfigureMemoryOptions(options, sku, numCores);

            Assert.Equal(expectedMemory, options.TotalAvailableMemoryBytes);
        }
    }
}
