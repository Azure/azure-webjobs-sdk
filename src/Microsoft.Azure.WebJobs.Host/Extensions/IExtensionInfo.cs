// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs
{
    public interface IExtensionInfo
    {
        /// <summary>
        /// Gets the friendly human readable name of the extension.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the resolved configuration section name for this extension.
        /// </summary>
        string ConfigurationSectionName { get; }
    }
}
