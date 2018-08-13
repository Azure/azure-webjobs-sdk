// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Extensions.Configuration
{
    public static class IConfigurationExtensions
    {
        public static string GetWebJobsConnectionString(this IConfiguration configuration, string connectionStringName)
        {
            // first try prefixing
            string prefixedConnectionStringName = GetPrefixedConnectionStringName(connectionStringName);
            string connectionString = FindConnectionString(configuration, prefixedConnectionStringName);

            if (string.IsNullOrEmpty(connectionString))
            {
                // next try a direct unprefixed lookup
                connectionString = FindConnectionString(configuration, connectionStringName);
            }

            return connectionString;
        }

        public static string GetPrefixedConnectionStringName(string connectionStringName)
        {
            return Constants.WebJobsConfigurationSectionName + connectionStringName;
        }
        private static string FindConnectionString(IConfiguration configuration, string connectionName) =>
            configuration.GetConnectionString(connectionName) ?? configuration[connectionName];
    }
}
