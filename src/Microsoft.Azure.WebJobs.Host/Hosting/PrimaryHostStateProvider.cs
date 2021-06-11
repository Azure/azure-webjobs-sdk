// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// Type was moved from https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script/Host/PrimaryHostStateProvider.cs

namespace Microsoft.Azure.WebJobs.Hosting
{
    internal class PrimaryHostStateProvider : IPrimaryHostStateProvider
    {
        public bool IsPrimary { get; set; }
    }
}
