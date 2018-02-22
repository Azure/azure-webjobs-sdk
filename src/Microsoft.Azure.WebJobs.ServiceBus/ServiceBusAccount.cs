// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Logging;
using System;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    internal class ServiceBusAccount
    {
        private ServiceBusConfiguration _config;
        private IConnectionProvider _connectionProvider;
        private string _connectionString;


        public ServiceBusAccount(ServiceBusConfiguration config, IConnectionProvider connectionProvider = null)
        {


            _config = config ?? throw new ArgumentNullException(nameof(config));
            _config = config;
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
                        _connectionString = AmbientConnectionStringProvider.Instance.GetConnectionString(_connectionProvider.Connection);
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
