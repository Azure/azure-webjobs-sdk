// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;

namespace Microsoft.Azure.WebJobs.Script.Config
{
    internal class HostingConfigOptionsSetup : IConfigureOptions<HostingConfigOptions>
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public HostingConfigOptionsSetup(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<HostingConfigOptions>();
        }

        public void Configure(HostingConfigOptions options)
        {
            IConfigurationSection section = _configuration.GetSection(Constants.HostingConfigSectionName);
            if (section != null)
            {
                foreach (var pair in section.GetChildren())
                {
                    if (!string.IsNullOrEmpty(pair.Value))
                    {
                        try
                        {
                            options.Features.Add(pair.Key, pair.Value);
                        }
                        catch (Exception ex)
                        {
                            // Best effort - log and continue
                            _logger.LogError(ex, $"Error adding hosting config for the pair: '{pair.Key}', '{pair.Value}'");
                        }
                    }
                }
            }
        }
    }
}