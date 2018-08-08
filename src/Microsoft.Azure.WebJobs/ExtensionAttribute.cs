// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Description
{
    /// <summary>
    /// Attribute applied to the <see cref="IExtensionConfigProvider"/> implementation for an extension.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ExtensionAttribute : Attribute
    {
        public ExtensionAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets the friendly name of the extension.
        /// </summary>
        public string Name { get;  }
    }
}
