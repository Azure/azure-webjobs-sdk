// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    /// <summary>
    /// Provides access to an instance-scoped <see cref="IServiceProvider"/>.
    /// </summary>
    public interface IInstanceServicesProvider
    {
        IServiceProvider InstanceServices { get; set; }
    }
}