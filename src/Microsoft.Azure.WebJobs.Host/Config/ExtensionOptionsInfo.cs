// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    public class ExtensionOptionsInfo<TOptions> : IExtensionOptionsInfo
    {
        private readonly ExtensionInfo _extensionInfo;
        public ExtensionOptionsInfo(ExtensionInfo extensionInfo)
        {
            _extensionInfo = extensionInfo;
        }
        public ExtensionInfo ExtensionInfo => _extensionInfo;

        public Type OptionType => typeof(TOptions);
    }
}
