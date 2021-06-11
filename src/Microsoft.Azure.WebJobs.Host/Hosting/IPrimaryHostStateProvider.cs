// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// Type was moved from https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script/Host/IPrimaryHostStateProvider.cs

namespace Microsoft.Azure.WebJobs.Hosting
{
    /// <summary>
    /// Provides access to the primary host state. When an application is running on multiple
    /// scaled out instances, only one instance will be primary.
    /// </summary>
    /// <remarks>
    /// See <see cref="PrimaryHostCoordinatorOptions"/> for more information.
    /// </remarks>
    public interface IPrimaryHostStateProvider
    {
        /// <summary>
        /// Gets or sets a value indicating whether the currently running host is "Primary".
        /// </summary>
        bool IsPrimary { get; set; }
    }
}
