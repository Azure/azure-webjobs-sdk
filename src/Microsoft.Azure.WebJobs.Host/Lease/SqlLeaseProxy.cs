// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Lease;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Host.Lease
{
    // Sql based lease implementation
    internal class SqlLeaseProxy : ILeaseProxy
    {
        public static bool IsSqlLeaseType()
        {
            try
            {
                string connectionString = GetConnectionString(ConnectionStringNames.Lease);

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    return false;
                }

                // Try creating a SQL connection. This will implicitly parse the connection string
                // and throw an exception if it fails.
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                }

                return true;
            }
            catch (Exception)
            {
            }

            return false;
        }

        /// <inheritdoc />
        public Task<string> TryAcquireLeaseAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<string> AcquireLeaseAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task RenewLeaseAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task WriteLeaseMetadataAsync(LeaseDefinition leaseDefinition, string key,
            string value, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<LeaseInformation> ReadLeaseInfoAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task ReleaseLeaseAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private static string GetConnectionString(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                throw new InvalidOperationException("Lease account name not specified");
            }

            return AmbientConnectionStringProvider.Instance.GetConnectionString(accountName);
        }
    }
}
