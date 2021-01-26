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
        /// <summary>
        /// Creates an instance of the <see cref="ExtensionAttribute"/>.
        /// </summary>
        /// <param name="name">The friendly, human readable, name of the extension.</param>
        /// <param name="configurationSection">The name of the configuration section for this extension.</param>
        public ExtensionAttribute(string name, string configurationSection = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ConfigurationSection = configurationSection;
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
