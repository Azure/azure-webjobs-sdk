// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Hosting
{
    /// <summary>
    /// Interface defining a trigger scale configuration action.
    /// as part of host startup.
    /// </summary>
    public interface IConfigureTriggerScale
    {
        /// <summary>
        /// Performs the scale configuration action for a trigger. The host will call this
        /// method at the right time during scale host initialization for each trigger.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> that can be used to
        /// configure the trigger scale.</param>
        /// <param name="triggerScaleContext">he <see cref="HostBuilderContext"/> hat can be used to
        /// configure the trigger scale.</param>
        void ConfigureTriggerScale(IHostBuilder builder, HostBuilderContext triggerScaleContext);
    }
}
