// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Options;
using System;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    internal class ExtensionOptionsProvider<TOptions> : IExtensionOptionsProvider
    {
        private IOptionsMonitor<TOptions> _optionsMonitor;
        public ExtensionOptionsProvider(ExtensionInfo extensionInfo, IOptionsMonitor<TOptions> optionsMonitor)
        {
            ExtensionInfo = extensionInfo;
            _optionsMonitor = optionsMonitor;
        }

        public ExtensionInfo ExtensionInfo { get; private set; }
        public object GetOptions() => _optionsMonitor.CurrentValue;
    }
}
