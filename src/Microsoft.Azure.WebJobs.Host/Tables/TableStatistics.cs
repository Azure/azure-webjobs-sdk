// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Tables
{
    /// <summary>
    /// The table statistics
    /// </summary>
    public class TableStatistics
    {
        /// <summary>
        /// Measure # of rows read/written, and time spent in IO.
        /// </summary>
        public int ReadCount;

        /// <summary>
        /// The read IO time.
        /// </summary>
        public Stopwatch ReadIOTime = new Stopwatch();

        /// <summary>
        /// If Write IO time is high, likely culprit is bad batching due to poor partition key spread.
        /// </summary>
        public int WriteCount;

        /// <summary>
        /// The write IO time
        /// </summary>
        public Stopwatch WriteIOTime = new Stopwatch();

        /// <summary>
        /// The delete count
        /// </summary>
        public int DeleteCount;

        /// <summary>
        /// The delete IO time.
        /// </summary>
        public Stopwatch DeleteIOTime = new Stopwatch();

        /// <summary>
        /// Gets the status.
        /// </summary>
        /// <returns></returns>
        public string GetStatus()
        {
            StringBuilder sb = new StringBuilder();
            if (ReadCount > 0)
            {
                sb.AppendFormat("Read {0} rows. ({1} time) ", ReadCount, ReadIOTime.Elapsed);
            }
            if (WriteCount > 0)
            {
                sb.AppendFormat("Wrote {0} rows. ({1} time) ", WriteCount, WriteIOTime.Elapsed);
            }
            if (DeleteCount > 0)
            {
                sb.AppendFormat("Deleted {0} individual rows. ({1} time)", DeleteCount, DeleteIOTime.Elapsed);
            }

            if (sb.Length == 0)
            {
                return "No table activity.";
            }
            return sb.ToString();
        }

        /// <summary>
        /// The to string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return GetStatus();
        }
    }
}
