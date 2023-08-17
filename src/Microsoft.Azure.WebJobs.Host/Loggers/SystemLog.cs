// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    /// <summary>
    /// Class for controlling system log behavior.
    /// </summary>
    public static class SystemLog
    {
        private static readonly AsyncLocal<bool> Local = new AsyncLocal<bool>() { Value = true };

        /// <summary>
        /// Gets a value indicating whether system logs and enabled.
        /// </summary>
        public static bool Enabled => Local.Value;

        /// <summary>
        /// Disables system logs for the current async scope.
        /// </summary>
        /// <returns>A disposable that will revert to previous system log state on disposal.</returns>
        public static Disposable Disable() => new Disposable(false);

        /// <summary>
        /// Enables system logs the current async scope.
        /// </summary>
        /// <returns>A disposable that will revert to previous system log state on disposal.</returns>
        public static Disposable Enable() => new Disposable(true);

        /// <summary>
        /// A disposable that reverts system log enable state on disposal.
        /// </summary>
        public readonly struct Disposable : IDisposable
        {
            private readonly bool _original;

            public Disposable(bool target)
            {
                _original = Local.Value;
                Local.Value = target;
            }

            public void Dispose()
            {
                Local.Value = _original;
            }
        }
    }
}
