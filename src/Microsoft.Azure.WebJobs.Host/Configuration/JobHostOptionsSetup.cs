// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Configuration
{
    public class JobHostOptionsSetup : IConfigureOptions<JobHostOptions>
    {
        private readonly INameResolver _nameResolver;

        public JobHostOptionsSetup(INameResolver nameResolver)
        {
            _nameResolver = nameResolver;
        }

        public void Configure(JobHostOptions options)
        {
            options.NameResolver = _nameResolver;
        }
    }
}
