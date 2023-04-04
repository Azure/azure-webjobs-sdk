// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights.Extensions
{
    internal static class LogLevelExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToStringOptimized(this LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    return "Trace";
                case LogLevel.Debug:
                    return "Debug";
                case LogLevel.Information:
                    return "Information";
                case LogLevel.Warning:
                    return "Warning";
                case LogLevel.Error:
                    return "Error";
                case LogLevel.Critical:
                    return "Critical";
                case LogLevel.None:
                    return "None";
                default:
                    return logLevel.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
