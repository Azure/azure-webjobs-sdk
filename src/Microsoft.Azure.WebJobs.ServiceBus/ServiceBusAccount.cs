// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Logging;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    internal class ServiceBusAccount
    {
        private readonly ServiceBusOptions _config;
        private readonly IConnectionStringProvider _connectionStringProvider;
        private readonly IConnectionProvider _connectionProvider;
        private string _connectionString;


        public ServiceBusAccount(ServiceBusOptions config, IConnectionStringProvider connectionStringProvider, IConnectionProvider connectionProvider = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _connectionStringProvider = connectionStringProvider;
            _connectionProvider = connectionProvider;
        }

        internal ServiceBusAccount()
        {
        }

        public virtual string ConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(_connectionString))
                {
                    _connectionString = _config.ConnectionString;
                    if (_connectionProvider != null && !string.IsNullOrEmpty(_connectionProvider.Connection))
                    {
                        _connectionString = _connectionStringProvider.GetConnectionString(_connectionProvider.Connection);
                    }

                    if (string.IsNullOrEmpty(_connectionString))
                    {
                        var defaultConnectionName = "AzureWebJobs" + ConnectionStringNames.ServiceBus;
                        throw new InvalidOperationException(
                            string.Format(CultureInfo.InvariantCulture, "Microsoft Azure WebJobs SDK ServiceBus connection string '{0}' is missing or empty.",
                            Sanitizer.Sanitize(_connectionProvider.Connection) ?? defaultConnectionName));
                    }
                }

                return _connectionString;
            }
        }


    }
}
