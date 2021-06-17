// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class ConcurrencyStatusSnapshotTests
    {
        [Fact]
        public void Equals_ReturnsExpectedResult()
        {
            var snapshot1 = new HostConcurrencySnapshot();
            var snapshot2 = new HostConcurrencySnapshot();
            Assert.True(snapshot1.Equals(snapshot2));

            // timestamp not included in equality check
            snapshot1.Timestamp = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(15));
            snapshot2.Timestamp = DateTime.UtcNow;
            Assert.True(snapshot1.Equals(snapshot2));

            Assert.False(snapshot1.Equals(null));

            // differing top level properties
            snapshot1 = new HostConcurrencySnapshot
            {
                NumberOfCores = 1
            };
            snapshot2 = new HostConcurrencySnapshot
            {
                NumberOfCores = 4
            };
            Assert.False(snapshot1.Equals(snapshot2));

            snapshot1.NumberOfCores = snapshot2.NumberOfCores = 4;
            snapshot1.FunctionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>
            {
                { "function0", new FunctionConcurrencySnapshot { Concurrency = 5 } },
                { "function1", new FunctionConcurrencySnapshot { Concurrency = 10 } },
                { "function2", new FunctionConcurrencySnapshot { Concurrency = 15 } }
            };
            Assert.False(snapshot1.Equals(snapshot2));

            // different functions
            snapshot2.FunctionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>
            {
                { "function0", new FunctionConcurrencySnapshot { Concurrency = 5 } },
                { "function1", new FunctionConcurrencySnapshot { Concurrency = 10 } },
                { "function5", new FunctionConcurrencySnapshot { Concurrency = 15 } }
            };
            Assert.False(snapshot1.Equals(snapshot2));

            // same functions, but differences in function snapshot properties
            snapshot2.FunctionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>
            {
                { "function0", new FunctionConcurrencySnapshot { Concurrency = 1 } },
                { "function1", new FunctionConcurrencySnapshot { Concurrency = 10 } },
                { "function2", new FunctionConcurrencySnapshot { Concurrency = 50 } }
            };
            Assert.False(snapshot1.Equals(snapshot2));

            // everything equal
            snapshot2.FunctionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>
            {
                { "function0", new FunctionConcurrencySnapshot { Concurrency = 5 } },
                { "function1", new FunctionConcurrencySnapshot { Concurrency = 10 } },
                { "function2", new FunctionConcurrencySnapshot { Concurrency = 15 } }
            };
            Assert.True(snapshot1.Equals(snapshot2));
        }
    }
}
