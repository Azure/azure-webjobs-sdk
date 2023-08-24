// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    internal class PrimaryHostCoordinatorOptionsSetup : IConfigureOptions<PrimaryHostCoordinatorOptions>
    {
        private readonly IOptions<ConcurrencyOptions> _concurrencyOptions;

        public PrimaryHostCoordinatorOptionsSetup(IOptions<ConcurrencyOptions> concurrencyOptions)
        {
            _concurrencyOptions = concurrencyOptions;
        }

        public void Configure(PrimaryHostCoordinatorOptions options)
        {
            // in most WebJobs SDK scenarios, primary host coordination is not needed
            // however, some features require it
            if (_concurrencyOptions.Value.DynamicConcurrencyEnabled)
            {
                options.Enabled = true;
            }
        }
    }
}
