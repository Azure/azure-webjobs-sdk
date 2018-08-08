// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Host.Hosting
{
    internal class WebJobsExtensionBuilder : IWebJobsExtensionBuilder
    {
        public WebJobsExtensionBuilder(IServiceCollection services)
            : this(services, null)
        {
        }

        public WebJobsExtensionBuilder(IServiceCollection services, string extensionName)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            ExtensionName = extensionName ?? throw new ArgumentNullException(nameof(extensionName));
        }

        public IServiceCollection Services { get; }

        public string ExtensionName { get; }
    }
}
