﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Host.Bindings.StorageAccount
{
    internal class CloudStorageAccountBindingProvider : IBindingProvider
    {
        private readonly IStorageAccountProvider _accountProvider;

        public CloudStorageAccountBindingProvider(IStorageAccountProvider accountProvider)
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

            if (context.Parameter.ParameterType != typeof(CloudStorageAccount))
            {
                return null;
            }

            var attr = TypeUtility.GetAttr<StorageAccountAttribute>(parameter) ?? new StorageAccountAttribute(null);

            INameResolver nameResolver = null; // $$ bug?
            IStorageAccount account = await _accountProvider.GetStorageAccountAsync(attr, context.CancellationToken, nameResolver);
            IBinding binding = new CloudStorageAccountBinding(parameter.Name, account.SdkObject);
            return binding;
        }
    }
}
