// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// An interface for invoking function filters
    /// </summary>
    [CLSCompliant(false)]
    public interface IFunctionInvocationFilter
    {
        /// <summary>
        /// Called before the main function is called
        /// </summary>
        /// <returns></returns>
        Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken);

        /// <summary>
        /// Called after the main function is called
        /// </summary>
        /// <returns></returns>
        Task OnExecutedAsync(FunctionExecutedContext executedContext, CancellationToken cancellationToken);
    }
}
