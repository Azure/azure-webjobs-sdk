// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class WebJobsBuilder : IWebJobsBuilder
    {
        private readonly IServiceCollection _services;

        public WebJobsBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IServiceCollection Services => _services;
    }
}
