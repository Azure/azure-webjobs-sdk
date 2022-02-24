// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Hosting;
using System;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    public class ExtensionOptionsInfo : IExtensionOptionsInfo
    {   
        public ExtensionOptionsInfo(ExtensionInfo extensionInfo, IOptionsFormatter optionsFormatter)
        {
            ExtensionInfo = extensionInfo;
            OptionsFormatter = optionsFormatter;
        }

        public ExtensionInfo ExtensionInfo { get; private set; }
        public IOptionsFormatter OptionsFormatter { get; private set; }
    }
}
