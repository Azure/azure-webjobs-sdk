﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings.Runtime;
using Microsoft.Azure.WebJobs.Host.Storage;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal static class StorageAccountProviderExtensions
    {
        public static Task<IStorageAccount> GetDashboardAccountAsync(this IStorageAccountProvider provider, CancellationToken cancellationToken)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }

            return provider.TryGetAccountAsync(ConnectionStringNames.Dashboard, cancellationToken);
        }

        public static async Task<IStorageAccount> GetStorageAccountAsync(this IStorageAccountProvider provider, CancellationToken cancellationToken)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }

            IStorageAccount account = await provider.TryGetAccountAsync(ConnectionStringNames.Storage, cancellationToken);
            ValidateStorageAccount(account, ConnectionStringNames.Storage);
            return account;
        }

        public static async Task<IStorageAccount> GetAccountAsync(this IStorageAccountProvider provider, string connectionStringName, CancellationToken cancellationToken)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }

            IStorageAccount account = await provider.TryGetAccountAsync(connectionStringName, cancellationToken);
            ValidateStorageAccount(account, connectionStringName);
            return account;
        }
        public static async Task<IStorageAccount> GetStorageAccountAsync(this IStorageAccountProvider provider, ParameterInfo parameter, CancellationToken cancellationToken, INameResolver nameResolver = null)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }

            string connectionStringName = GetAccountOverrideOrNull(parameter);
            if (string.IsNullOrEmpty(connectionStringName))
            {
                connectionStringName = ConnectionStringNames.Storage;
            }

            if (nameResolver != null)
            {
                string resolved = null;
                if (nameResolver.TryResolveWholeString(connectionStringName, out resolved))
                {
                    connectionStringName = resolved;
                }
            }

            IStorageAccount account = await provider.TryGetAccountAsync(connectionStringName, cancellationToken);
            ValidateStorageAccount(account, connectionStringName);
            return account;
        }

        private static void ValidateStorageAccount(IStorageAccount account, string connectionStringName)
        {
            if (account == null)
            {
                string message = StorageAccountParser.FormatParseAccountErrorMessage(StorageAccountParseResult.MissingOrEmptyConnectionStringError, connectionStringName);
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Walk from the parameter up to the containing type, looking for a
        /// <see cref="StorageAccountAttribute"/>. If found, return the account.
        /// </summary>
        internal static string GetAccountOverrideOrNull(ParameterInfo parameter)
        {
            StorageAccountAttribute attribute = TypeUtility.GetHierarchicalAttributeOrNull<StorageAccountAttribute>(parameter);
            if (attribute != null)
            {
                return attribute.Account;
            }
            return null;
        }
    }
}
