// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace SampleHost
{
    /// <summary>
    /// Interface for providing status for dynamic listeners. A dynamic listener will be started and stopped
    /// by the platform based on the status returned by this provider.
    /// </summary>
    public interface IDynamicListenerStatusProvider
    {
        /// <summary>
        /// Determines whether the specified function listener should be dynamically managed.
        /// </summary>
        /// <param name="functionId">The function ID.</param>
        /// <returns>True if the listener is dynamic, false otherwise.</returns>
        bool IsDynamic(string functionId);

        /// <summary>
        /// Gets the status of the dynamic listener for the specified function.
        /// </summary>
        /// <param name="functionId">The function ID.</param>
        /// <returns>The dynamic listener status.</returns>
        Task<DynamicListenerStatus> GetStatusAsync(string functionId);

        /// <summary>
        /// Method called when a listener has stopped and should be disposed.
        /// </summary>
        /// <param name="functionId">The function ID.</param>
        /// <param name="listener"></param>
        /// <remarks>
        /// This method allows the provider to control when the listener actually gets
        /// disposed.
        /// </remarks>
        void DisposeListener(string functionId, IListener listener);
    }
}
