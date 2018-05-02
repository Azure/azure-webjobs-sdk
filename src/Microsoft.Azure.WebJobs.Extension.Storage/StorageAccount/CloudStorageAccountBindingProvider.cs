// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Host.Bindings.StorageAccount
{
    internal class CloudStorageAccountBindingProvider : IBindingProvider
    {
        private readonly XStorageAccountProvider _accountProvider;

        public CloudStorageAccountBindingProvider(XStorageAccountProvider accountProvider)
        {
            if (accountProvider == null)
            {
                throw new ArgumentNullException("accountProvider");
            }

            _accountProvider = accountProvider;
        }

        public async Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            ParameterInfo parameter = context.Parameter;

            if (parameter.ParameterType != typeof(CloudStorageAccount))
            {
                return null;
            }

            var accountAttribute = TypeUtility.GetHierarchicalAttributeOrNull<StorageAccountAttribute>(parameter);
            var account = _accountProvider.Get(accountAttribute?.Account);

            return new CloudStorageAccountBinding(parameter.Name, account.SdkObject);
        }
    }
}
