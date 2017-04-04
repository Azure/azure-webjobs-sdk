// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Runtime.ExceptionServices;

namespace Microsoft.Azure.WebJobs.Host.Timers
{
    /// <summary>
    /// Helper methods for IWebJobsExceptionHandler
    /// </summary>
    public static class IWebJobsExceptionHandlerExtensions
    {
        /// <summary>
        /// Captures the exception and synchronously runs OnUnhandledExceptionAsync
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="e"></param>
        public static void Capture(this IWebJobsExceptionHandler handler, Exception e)
        {
            var info = ExceptionDispatchInfo.Capture(e);
            handler?.OnUnhandledExceptionAsync(info).GetAwaiter().GetResult();
        }
    }
}
