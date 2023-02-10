// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    public class HostingConfigOptions
    {
        private readonly Dictionary<string, string> _features;

        public HostingConfigOptions()
        {
            _features = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets all features in the hosting configuration.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetFeatures => _features;

        /// <summary>
        /// Gets feature by name.
        /// </summary>
        /// <param name="name">Feature name.</param>
        /// <returns>String value from hostig configuration.</returns>
        public string GetFeature(string name)
        {
            if (_features.TryGetValue(name, out string value))
            {
                return value;
            }
            return null;
        }

        internal Dictionary<string, string> Features => _features;
    }
}
