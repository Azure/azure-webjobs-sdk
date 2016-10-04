// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.WebJobs.Logging
{
    /// <summary>
    /// Factory object to instantiate new instances of the logging interfaces
    /// </summary>
    public static class LogFactory
    {
        /// <summary>
        /// Get a reader that reads from the given table. 
        /// </summary>
        /// <param name="tableLookup"></param>
        /// <returns></returns>
        public static ILogReader NewReader(IEpochTableProvider tableLookup)
        {
            return new LogReader(tableLookup);
        }

        /// <summary>
        /// Create a new writer for the given compute container name that writes to the given table.
        /// Multiple compute instances can write to the same table simultaneously without interference. 
        /// </summary>
        /// <param name="computerContainerName">name of the compute container. Likley %COMPUTERNAME%. </param>
        /// <param name="tableLookup">underlying azure storage table to write to. 
        /// Passed a string suffix (which will consist of valid Azure table characters). 
        /// This must be a deterministic (replayable) function.</param>
        /// <returns></returns>
        public static ILogWriter NewWriter(string computerContainerName, IEpochTableProvider tableLookup)
        {
            return new LogWriter(computerContainerName, tableLookup);
        }

        /// <summary>
        /// Get a default table provider for the given tableClient. This will generate table names with the given prefix.
        /// </summary>
        /// <param name="tableClient">storage client for where to generate tables</param>
        /// <param name="tableNamePrefix">prefix for tables to generate. This should be a valid azure table name.</param>
        public static IEpochTableProvider NewTableProvider(CloudTableClient tableClient, string tableNamePrefix = LogFactory.DefaultLogTableName)
        {
            return new EpochTableProvider(tableClient, tableNamePrefix);
        }

        /// <summary>
        /// Default name for fast log tables.
        /// </summary>
        public const string DefaultLogTableName = "AzureWebJobsHostLogs";
    }
}
