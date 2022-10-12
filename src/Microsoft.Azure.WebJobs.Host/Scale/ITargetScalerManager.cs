// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Manager for registering and accessing <see cref="ITargetScaler"/> instances for
    /// a <see cref="JobHost"/> instance.
    /// </summary>
    public interface ITargetScalerManager
    {
        /// <summary>
        /// Register an <see cref="ITargetScaler"/> instance.
        /// </summary>
        /// <param name="scaler">The target scaler instance to register.</param>
        void Register(ITargetScaler scaler);

        /// <summary>
        /// Get all registered target scaler instances.
        /// </summary>
        /// <remarks>
        /// Should only be called after the host has been started and all
        /// instances are registered.
        /// </remarks>
        /// <returns>The collection of target scaler instances.</returns>
        IEnumerable<ITargetScaler> GetTargetScalers();
    }
}
