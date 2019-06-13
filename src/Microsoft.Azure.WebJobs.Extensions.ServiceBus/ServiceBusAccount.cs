// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    internal class ServiceBusAccount
    {
        private readonly ServiceBusOptions _options;
        private readonly IConnectionProvider _connectionProvider;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly string _entityPath;
        private readonly bool _isSessionsEnabled;

        public ServiceBusAccount(ServiceBusOptions options, IConfiguration configuration, string entityPath, IConnectionProvider connectionProvider = null, bool isSessionsEnabled = false)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _configuration = configuration;
            _connectionProvider = connectionProvider;
            _entityPath = entityPath;
            _isSessionsEnabled = isSessionsEnabled;
            _connectionString = _options.ConnectionString;

            if (_connectionProvider != null && !string.IsNullOrEmpty(_connectionProvider.Connection))
            {
                _connectionString = _configuration.GetWebJobsConnectionString(_connectionProvider.Connection);
            }

            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture, "Microsoft Azure WebJobs SDK ServiceBus connection string '{0}' is missing or empty.",
                    Sanitizer.Sanitize(_connectionProvider.Connection) ?? "AzureWebJobsServiceBus"));
            }

            ServiceBusConnectionStringBuilder builder = new ServiceBusConnectionStringBuilder(_connectionString);
            if (!string.IsNullOrEmpty(builder.EntityPath))
            {
                if (!string.IsNullOrEmpty(entityPath) && builder.EntityPath != entityPath)
                {
                    throw new InvalidOperationException("Entity path in connection string does not match entity in ServiceBus trigger definition");
                }
                _entityPath = builder.EntityPath;
                _connectionString = builder.GetNamespaceConnectionString();
            }
        }

        internal ServiceBusAccount()
        {
        }

        public virtual string ConnectionString
        {
            get
            {
                return _connectionString;
            }
        }

        public virtual string EntityPath
        {
            get
            {
                return _entityPath;
            }
        }

        public bool IsSessionsEnabled
        {
            get
            {
                return _isSessionsEnabled;
            }
        }
    }
}
