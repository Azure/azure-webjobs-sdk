// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Config
{
    public interface IExtensionOptionsProvider
    {
        ExtensionInfo ExtensionInfo { get; }
        object GetOptions();
    }
}
