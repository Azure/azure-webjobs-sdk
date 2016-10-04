// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.Logging
{
    // A "time bucket" is a discrete unit of time that's useful for aggregation and reporting. 
    // Use minutes since a baseline.  
    internal static class TimeBucket
    {
        static DateTime _baselineTime = new DateTime(2000, 1, 1);

        public static DateTime ConvertToDateTime(long bucket)
        {
            return _baselineTime.AddMinutes(bucket);
        }

        public static long ConvertToBucket(DateTime dt)
        {
            TimeSpan ts = dt - _baselineTime;

            var min = (long)ts.TotalMinutes;
            if (min < 0)
            {
                return 0;
            }
            return min;
        }

        // USed to compute epoch. 
        const long ticksBaseline = 633663648000000000L;
        const long ticksPerMonth = 26785144663987L;

        public static DateTime CommonEpoch = new DateTime(0, DateTimeKind.Utc);

        public static long GetEpochNumberFromTable(CloudTable table)
        {
            string tableName = table.Name;
            if (tableName.Length < 5)
            {
                return -1;
            }
            string suffix = tableName.Substring(tableName.Length - 5, 5);
            long epoch;
            if (long.TryParse(suffix, out epoch))
            {
                return epoch;
            }
            return -1;
        }

        // Epoch must be positive. 
        public static long GetEpochNumber(DateTime epoch)
        {
            var ts = (epoch.Ticks - ticksBaseline) / ticksPerMonth;
            if (ts < 0)
            {
                ts = 0;
            }
            return ts;
        }

        public static CloudTable GetTableForEpoch(this IEpochTableProvider tableLookup, DateTime epoch)
        {
            // Epoch(DateTime.MaxValue) is 94146, still a 5 digit number. 
            var ts = GetEpochNumber(epoch);
            string suffix = string.Format(CultureInfo.InvariantCulture, "{0:D5}", ts);
            var table = tableLookup.NewTable(suffix);
            return table;
        }

        public static CloudTable GetTableForEpoch(this IEpochTableProvider tableLookup, long timeBucket)
        {
            var time = ConvertToDateTime(timeBucket);
            return tableLookup.GetTableForEpoch(time);
        }

    }
}
