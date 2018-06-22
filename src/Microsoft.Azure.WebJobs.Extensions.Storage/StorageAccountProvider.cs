// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs
{    
    /// <summary>
    /// Abstraction to provide storage accounts from the connection names. 
    /// This gets the storage account name via the binding attribute's <see cref="IConnectionProvider.Connection"/>
    /// property. 
    /// If the connection is not specified on the attribute, it uses a default account. 
    /// </summary>
    public class StorageAccountProvider
    {
        private readonly IConnectionStringProvider _provider;

        // $$$ This uses the IConnectionStringProvider  for backwards compat, but we really should 
        // get rid of that and plumb it entirely through DI IConfiguration. 
        public StorageAccountProvider(IConnectionStringProvider provider)
        {
            _provider = provider;
        }

        public StorageAccount Get(string name, INameResolver resolver)
        {
            var resolvedName = resolver.ResolveWholeString(name);
            return this.Get(resolvedName);
        }

        public virtual StorageAccount Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Storage"; // default
            }

            // $$$ Where does validation happen? 
            string cx = _provider.GetConnectionString(name);
            if (cx == null)
            {
                // Not found
                throw new InvalidOperationException($"Storage account '{name}' is not configured.");
            }

            return StorageAccount.NewFromConnectionString(cx);
        }

        /// <summary>
        /// The host account is for internal storage mechanisms like load balancer queuing. 
        /// </summary>
        /// <returns></returns>
        public virtual StorageAccount GetHost()
        {
            return this.Get(null);
        }
    }
}