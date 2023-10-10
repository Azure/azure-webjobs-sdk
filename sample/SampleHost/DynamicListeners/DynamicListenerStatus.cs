// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace SampleHost
{
    /// <summary>
    /// Represents the status of a dynamic listener. See <see cref="IDynamicListenerStatusProvider.GetStatusAsync(string)"/>.
    /// </summary>
    public class DynamicListenerStatus
    {
        /// <summary>
        /// Gets or sets the next polling interval for listener status.
        /// </summary>
        public TimeSpan NextInterval { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the listener should be running.
        /// </summary>
        public bool IsEnabled { get; set; }
    }
}
