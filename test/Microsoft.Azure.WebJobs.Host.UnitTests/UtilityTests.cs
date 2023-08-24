// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class UtilityTests
    {
        [Fact]
        [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
        public void TakeLastN_ReturnsExpectedValues()
        {
            int[] values = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                values.TakeLastN(-1);
            });

            var result = values.TakeLastN(0);
            Assert.Empty(result);

            result = values.TakeLastN(1);
            Assert.Collection(result,
                p => Assert.Equal(10, p));

            result = values.TakeLastN(5);
            Assert.Collection(result,
                p => Assert.Equal(6, p),
                p => Assert.Equal(7, p),
                p => Assert.Equal(8, p),
                p => Assert.Equal(9, p),
                p => Assert.Equal(10, p));

            result = values.TakeLastN(10);
            Assert.Collection(result,
                p => Assert.Equal(1, p),
                p => Assert.Equal(2, p),
                p => Assert.Equal(3, p),
                p => Assert.Equal(4, p),
                p => Assert.Equal(5, p),
                p => Assert.Equal(6, p),
                p => Assert.Equal(7, p),
                p => Assert.Equal(8, p),
                p => Assert.Equal(9, p),
                p => Assert.Equal(10, p));

            result = values.TakeLastN(15);
            Assert.Collection(result,
                p => Assert.Equal(1, p),
                p => Assert.Equal(2, p),
                p => Assert.Equal(3, p),
                p => Assert.Equal(4, p),
                p => Assert.Equal(5, p),
                p => Assert.Equal(6, p),
                p => Assert.Equal(7, p),
                p => Assert.Equal(8, p),
                p => Assert.Equal(9, p),
                p => Assert.Equal(10, p));
        }

        [Fact]
        public void FlattenException_AggregateException_ReturnsExpectedResult()
        {
            ApplicationException ex1 = new ApplicationException("Incorrectly configured setting 'Foo'");
            ex1.Source = "Acme.CloudSystem";

            // a dupe of the first
            ApplicationException ex2 = new ApplicationException("Incorrectly configured setting 'Foo'");
            ex1.Source = "Acme.CloudSystem";

            AggregateException aex = new AggregateException("One or more errors occurred.", ex1, ex2);

            string formattedResult = Utility.FlattenException(aex);
            Assert.Equal("Acme.CloudSystem: Incorrectly configured setting 'Foo'.", formattedResult);
        }

        [Fact]
        public void FlattenException_SingleException_ReturnsExpectedResult()
        {
            ApplicationException ex = new ApplicationException("Incorrectly configured setting 'Foo'");
            ex.Source = "Acme.CloudSystem";

            string formattedResult = Utility.FlattenException(ex);
            Assert.Equal("Acme.CloudSystem: Incorrectly configured setting 'Foo'.", formattedResult);
        }

        [Fact]
        public void FlattenException_MultipleInnerExceptions_ReturnsExpectedResult()
        {
            ApplicationException ex1 = new ApplicationException("Exception message 1");
            ex1.Source = "Source1";

            ApplicationException ex2 = new ApplicationException("Exception message 2.", ex1);
            ex2.Source = "Source2";

            ApplicationException ex3 = new ApplicationException("Exception message 3", ex2);

            string formattedResult = Utility.FlattenException(ex3);
            Assert.Equal("Exception message 3. Source2: Exception message 2. Source1: Exception message 1.", formattedResult);
        }

        [Fact]
        public void GetEffectiveCoresCount_ReturnsExpectedResult()
        {
            string prevSku = Environment.GetEnvironmentVariable(Constants.AzureWebsiteSku);
            string prevRoleInstanceId = Environment.GetEnvironmentVariable("RoleInstanceId");

            try
            {
                Assert.Equal(Environment.ProcessorCount, Utility.GetEffectiveCoresCount());

                Environment.SetEnvironmentVariable(Constants.AzureWebsiteSku, Constants.DynamicSku);
                Assert.Equal(1, Utility.GetEffectiveCoresCount());

                Environment.SetEnvironmentVariable("RoleInstanceId", "dw0SmallDedicatedWebWorkerRole_hr0HostRole -0-VM-1");
                Assert.Equal(Environment.ProcessorCount, Utility.GetEffectiveCoresCount());
            }
            finally
            {
                Environment.SetEnvironmentVariable(Constants.AzureWebsiteSku, prevSku);
                Environment.SetEnvironmentVariable("RoleInstanceId", prevRoleInstanceId);
            }
        }
    }
}
