﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Connection string provider that reads from configuration first, and if a connection
    /// is not found there, will search in environment variables.
    /// </summary>
    public class AmbientConnectionStringProvider : IConnectionStringProvider
    {
        private static readonly AmbientConnectionStringProvider Singleton = new AmbientConnectionStringProvider();

        internal static readonly string Prefix = "AzureWebJobs";

        private AmbientConnectionStringProvider()
        {
        }

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static AmbientConnectionStringProvider Instance
        {
            get { return Singleton; }
        }

        /// <summary>
        /// Attempts to first read a connection string from the connectionStrings configuration section.
        /// If not found there, it will attempt to read from environment variables.
        /// </summary>
        /// <param name="connectionStringName">The name of the connection string to look up.</param>
        /// <returns>The connection string, or <see langword="null"/> if no connection string was found.</returns>
        public string GetConnectionString(string connectionStringName)
        {
            // first try prefixing
            string prefixedConnectionStringName = GetPrefixedConnectionStringName(connectionStringName);
            string connectionString = ConfigurationUtility.GetConnectionString(prefixedConnectionStringName);

            if (string.IsNullOrEmpty(connectionString))
            {
                // next try a direct unprefixed lookup
                connectionString = ConfigurationUtility.GetConnectionString(connectionStringName);
            }

            return connectionString;
        }

        internal static string GetPrefixedConnectionStringName(string connectionStringName)
        {
            return Prefix + connectionStringName;
        }
    }
}
