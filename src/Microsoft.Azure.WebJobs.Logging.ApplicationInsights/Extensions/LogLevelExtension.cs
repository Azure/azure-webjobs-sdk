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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryParseOptimized(this string logLevel, out LogLevel level)
        {
            switch (logLevel)
            {
                case "Trace":
                    level = LogLevel.Trace;
                    break;
                case "Debug":
                    level = LogLevel.Debug;
                    break;
                case "Information":
                    level = LogLevel.Information;
                    break;
                case "Warning":
                    level = LogLevel.Warning;
                    break;
                case "Error":
                    level = LogLevel.Error;
                    break;
                case "Critical":
                    level = LogLevel.Critical;
                    break;
                case "None":
                    level = LogLevel.None;
                    break;
                default:
                    if (!Enum.TryParse(logLevel, out level))
                    {
                        return false;
                    }
                    break;
            }
            return true;
        }
    }
}
