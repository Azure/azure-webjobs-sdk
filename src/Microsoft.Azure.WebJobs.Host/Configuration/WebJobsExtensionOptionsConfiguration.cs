// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Configuration
{
    internal class WebJobsExtensionOptionsConfiguration<TOptions> : IConfigureOptions<TOptions> where TOptions : class
    {
        private readonly string _extensionName;
        private readonly IConfiguration _configuration;
        private readonly Action<IConfigurationSection, TOptions> _configure;

        public WebJobsExtensionOptionsConfiguration(IConfiguration configuration, string extensionName, Action<IConfigurationSection, TOptions> configure)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _configure = configure ?? throw new ArgumentNullException(nameof(configure));
            _extensionName = extensionName;
        }

        public void Configure(TOptions options)
        {
            IConfigurationSection section = Utility.GetExtensionConfigurationSection(_configuration, _extensionName);
            _configure(section, options);
        }
    }
}
