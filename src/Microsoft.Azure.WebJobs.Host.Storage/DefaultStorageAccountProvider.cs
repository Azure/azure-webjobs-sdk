// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

#if false // $$$
namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class DefaultStorageAccountProvider : IStorageAccountProvider
    {
        private readonly IConnectionStringProvider _ambientConnectionStringProvider;
        private readonly IStorageCredentialsValidator _storageCredentialsValidator;
        private readonly IStorageAccountParser _storageAccountParser;
        private readonly ConcurrentDictionary<string, IStorageAccount> _accounts = new ConcurrentDictionary<string, IStorageAccount>();

        private IStorageAccount _dashboardAccount;
        private bool _dashboardAccountSet;
        private IStorageAccount _storageAccount;
        private bool _storageAccountSet;

        public DefaultStorageAccountProvider(IConnectionStringProvider ambientConnectionStringProvider,
            IStorageAccountParser storageAccountParser, IStorageCredentialsValidator storageCredentialsValidator)
        {
            _ambientConnectionStringProvider = ambientConnectionStringProvider ?? throw new ArgumentNullException(nameof(ambientConnectionStringProvider));
            _storageCredentialsValidator = storageCredentialsValidator ?? throw new ArgumentNullException(nameof(storageCredentialsValidator));
            _storageAccountParser = storageAccountParser ?? throw new ArgumentNullException(nameof(storageAccountParser));
        }

        /// <summary>Gets or sets the Azure Storage connection string used for logging and diagnostics.</summary>
        public string DashboardConnectionString
        {
            get
            {
                if (!_dashboardAccountSet)
                {
                    return _ambientConnectionStringProvider.GetConnectionString(ConnectionStringNames.Dashboard);
                }

                // Intentionally access the field rather than the property to avoid setting _dashboardAccountSet.
                return _dashboardAccount != null ? _dashboardAccount.ToString(exportSecrets: true) : null;
            }
            set
            {
                DashboardAccount = !String.IsNullOrEmpty(value) ? ParseAccount(ConnectionStringNames.Dashboard, value) : null;
            }
        }

        /// <summary>Gets or sets the Azure Storage connection string used for reading and writing data.</summary>
        public string StorageConnectionString
        {
            get
            {
                if (!_storageAccountSet)
                {
                    return _ambientConnectionStringProvider.GetConnectionString(ConnectionStringNames.Storage);
                }

                // Intentionally access the field rather than the property to avoid setting _storageAccountSet.
                return _storageAccount != null ? _storageAccount.ToString(exportSecrets: true) : null;
            }
            set
            {
                StorageAccount = !String.IsNullOrEmpty(value) ? ParseAccount(ConnectionStringNames.Storage, value) : null;
            }
        }

        /// <summary>
        /// Get a SAS connection to a blob Container for use with SDK internal operations. 
        /// </summary>
        public string InternalSasStorage
        {
            get
            {
                return _ambientConnectionStringProvider.GetConnectionString(ConnectionStringNames.InternalSasStorage);
            }
        }

        private IStorageAccount DashboardAccount
        {
            get
            {
                if (!_dashboardAccountSet)
                {
                    _dashboardAccount = ParseAccount(ConnectionStringNames.Dashboard);
                    _dashboardAccountSet = true;
                }

                return _dashboardAccount;
            }
            set
            {
                _dashboardAccount = value;
                _dashboardAccountSet = true;
            }
        }

        private IStorageAccount StorageAccount
        {
            get
            {
                if (!_storageAccountSet)
                {
                    _storageAccount = ParseAccount(ConnectionStringNames.Storage);
                    _storageAccountSet = true;
                }

                return _storageAccount;
            }
            set
            {
                _storageAccount = value;
                _storageAccountSet = true;
            }
        }

        private async Task<IStorageAccount> CreateAndValidateAccountAsync(string connectionStringName, CancellationToken cancellationToken)
        {
            IStorageAccount account = null;
            var isPrimary = true;
            if (connectionStringName == ConnectionStringNames.Dashboard)
            {
                account = DashboardAccount;
            }
            else if (connectionStringName == ConnectionStringNames.Storage)
            {
                account = StorageAccount;
            }
            else
            {
                // see if this is a user connnection string (i.e. for multi-account scenarios)
                string connectionString = _ambientConnectionStringProvider.GetConnectionString(connectionStringName);
                if (!string.IsNullOrEmpty(connectionString))
                {
                    account = ParseAccount(connectionStringName, connectionString);
                    isPrimary = false;
                }
            }

            if (account != null)
            {
                await _storageCredentialsValidator.ValidateCredentialsAsync(account, cancellationToken);

                if (isPrimary)
                {
                    account.AssertTypeOneOf(StorageAccountType.GeneralPurpose);
                }
            }

            return account;
        }

        public async Task<IStorageAccount> TryGetAccountAsync(string connectionStringName, CancellationToken cancellationToken)
        {
            IStorageAccount account;
            if (!_accounts.TryGetValue(connectionStringName, out account))
            {
                // in rare cases createAndValidateAccountAsync could be called multiple times for the same account
                account = await CreateAndValidateAccountAsync(connectionStringName, cancellationToken);
                _accounts.AddOrUpdate(connectionStringName, (cs) => account, (cs, a) => account);
            }
            return account;
        }

        private IStorageAccount ParseAccount(string connectionStringName)
        {
            string connectionString = _ambientConnectionStringProvider.GetConnectionString(connectionStringName);
            return ParseAccount(connectionStringName, connectionString);
        }

        private IStorageAccount ParseAccount(string connectionStringName, string connectionString)
        {
            return _storageAccountParser.ParseAccount(connectionString, connectionStringName);
        }
    }
}

#endif