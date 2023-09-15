// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Hosting
{
    /// <summary>
    /// Interface defining a startup configuration action that should be performed
    /// as part of scale host startup.
    /// </summary>
    public interface IWebJobsTriggerStartup
    {
        /// <summary>
        /// Performs the startup configuration action. The host will call this
        /// method at the right time during host initialization.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> that can be used to
        /// configure the host.</param>
        /// <paramref name="triggerMetadata"/>The <see cref="TriggerMetadata"/> that used to
        /// configure the host.</param>
        void Configure(IHostBuilder builder, TriggerMetadata triggerMetadata);
    }
}
