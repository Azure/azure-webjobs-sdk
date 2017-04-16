// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Timers
{
    /// <summary>
    /// Helper methods for IWebJobsExceptionHandler
    /// </summary>
    public static class IWebJobsExceptionHandlerExtensions
    {
        /// <summary>
        /// Captures the exception and runs OnUnhandledExceptionAsync
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="e"></param>
        public static Task HandleAsync(this IWebJobsExceptionHandler handler, Exception e)
        {
            var info = ExceptionDispatchInfo.Capture(e);
            return handler?.OnUnhandledExceptionAsync(info) ?? Task.CompletedTask;
        }
    }
}
