// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    /// <summary>
    /// Represents hosting confgiuration.
    /// </summary>
    public class FunctionsHostingConfigOptions
    {
        private readonly Dictionary<string, string> _features;

        public FunctionsHostingConfigOptions()
        {
            _features = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets all features in the hosting configuration.
        /// </summary>
        public Dictionary<string, string> Features => _features;

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
    }
}
