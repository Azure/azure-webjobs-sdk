// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// Defines methods for binding to a value after an error was thrown.
    /// </summary>
    public interface IErrorAwareValueBinder : IValueBinder
    {
        /// <summary>
        /// Sets the error.
        /// </summary>
        /// <param name="value">The value / state.</param>
        /// <param name="error">The error thrown.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
        /// <returns>A <see cref="Task"/> for the operation.</returns>
        Task SetErrorAsync(object value, Exception error, CancellationToken cancellationToken);
    }
}
