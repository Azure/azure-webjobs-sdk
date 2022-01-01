// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Hosting
{
    public class WebJobsExtensionOptionProvider : ConfigurationProvider
    {
        public const string WebJobsExtensionOptionProviderKey = "WebJobsExtensionOptionProviderKey";
        public WebJobsExtensionOptionProvider()
        {
            WebJobsExtensionOptionRegistry.Subscribe(nameof(WebJobsExtensionOptionProvider), Load);
        }

        public override void Load()
        {
            if (Data.ContainsKey(WebJobsExtensionOptionProviderKey))
            {
                Data.Remove(WebJobsExtensionOptionProviderKey);
            }
            Data.Add(WebJobsExtensionOptionProviderKey, JsonConvert.SerializeObject(WebJobsExtensionOptionRegistry.GetOptions()));
        }
    }
}
