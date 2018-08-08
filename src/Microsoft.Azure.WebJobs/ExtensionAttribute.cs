// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Description
{
    /// <summary>
    /// Attribute used to mark types belonging to a single logical extension.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ExtensionAttribute : Attribute
    {
        public ExtensionAttribute(string name, string configurationSection = null)
        {
            Name = name;
            ConfigurationSection = configurationSection ?? name;
        }

        /// <summary>
        /// Gets the friendly human readable name of the extension.
        /// </summary>
        public string Name { get;  }

        /// <summary>
        /// Gets the configuration section name for this extension.
        /// </summary>
        public string ConfigurationSection { get; }
    }
}
