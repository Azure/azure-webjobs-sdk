// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights.Extensions
{
    internal class LogLevelEnumHelper
    {
        private static readonly string[] LogLevelEnumStrings;

        static LogLevelEnumHelper()
        {
            LogLevelEnumStrings = GetEnumOrderedStrings();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToStringOptimized(LogLevel enumValue)
        {
            return LogLevelEnumStrings[(int)enumValue];
        }
        private static string[] GetEnumOrderedStrings()
        {
            var list = Enum.GetValues(typeof(LogLevel));
            string[] enumOrderedStrings = new string[list.Length];
            int index = 0;
            foreach (var item in list)
            {
                enumOrderedStrings[index] = item.ToString();
                index++;
            }
            return enumOrderedStrings;
        }
    }
}
