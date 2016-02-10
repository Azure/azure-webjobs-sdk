// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using System;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    public static class EventHubJobHostConfigurationExtensions
    {
        public static void UseEventHub(this JobHostConfiguration config, EventHubConfiguration eventHubConfig)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (eventHubConfig == null)
            {
                throw new ArgumentNullException("eventHubConfig");
            }

            IExtensionRegistry extensions = config.GetService<IExtensionRegistry>();
            extensions.RegisterExtension<IExtensionConfigProvider>(eventHubConfig);
        }
    }
}