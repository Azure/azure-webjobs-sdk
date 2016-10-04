// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.WebJobs.Logging
{
    /// <summary>
    /// Logs are stored across multiple tables to aide in purging old logs.  
    /// </summary>
    public interface IEpochTableProvider
    {
        /// <summary>
        /// Return a table with the given suffix. The logging will create this table if it doesn't exist.
        /// </summary>
        /// <param name="suffix">Suffix will be legal table name characters. </param>
        CloudTable NewTable(string suffix);

        /// <summary>
        /// List all tables that we may have handed out. 
        /// Each table is a month's worth of data, so this is expected to be a small set. 
        /// </summary>
        /// <returns></returns>
        Task<CloudTable[]> ListTablesAsync();
    }
}