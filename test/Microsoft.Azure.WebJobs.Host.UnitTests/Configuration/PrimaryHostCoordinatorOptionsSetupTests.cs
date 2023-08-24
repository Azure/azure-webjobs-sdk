// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Configuration
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class PrimaryHostCoordinatorOptionsSetupTests
    {
        private readonly ConcurrencyOptions _concurrencyOptions;
        private readonly PrimaryHostCoordinatorOptionsSetup _setup;

        public PrimaryHostCoordinatorOptionsSetupTests()
        {
            _concurrencyOptions = new ConcurrencyOptions();
            var optionsWrapper = new OptionsWrapper<ConcurrencyOptions>(_concurrencyOptions);

            _setup = new PrimaryHostCoordinatorOptionsSetup(optionsWrapper);
        }

        [Fact]
        public void OptionsConstructor_ConfiguresExpectedDefaults()
        {
            var options = new PrimaryHostCoordinatorOptions();
            Assert.False(options.Enabled);
            Assert.Equal(TimeSpan.FromSeconds(15), options.LeaseTimeout);
        }

        [Fact]
        public void Configure_ConfiguresExpectedValues()
        {
            _concurrencyOptions.DynamicConcurrencyEnabled = false;
            var options = new PrimaryHostCoordinatorOptions();
            _setup.Configure(options);
            Assert.False(options.Enabled);

            _concurrencyOptions.DynamicConcurrencyEnabled = true;
            _setup.Configure(options);
            Assert.True(options.Enabled);
        }
    }
}
