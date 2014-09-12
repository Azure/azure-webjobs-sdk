﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.WebJobs.Host.Tables
{
    internal class CloudTableArgumentBindingProvider : ITableArgumentBindingProvider
    {
        public ITableArgumentBinding TryCreate(Type parameterType)
        {
            if (parameterType != typeof(CloudTable))
            {
                return null;
            }

            return new CloudTableArgumentBinding();
        }

        private class CloudTableArgumentBinding : ITableArgumentBinding
        {
            public Type ValueType
            {
                get { return typeof(CloudTable); }
            }

            public async Task<IValueProvider> BindAsync(CloudTable value, ValueBindingContext context)
            {
                await value.CreateIfNotExistsAsync(context.CancellationToken);
                return new TableValueProvider(value, value, typeof(CloudTable));
            }

            public FileAccess Access
            {
                get
                {
                    return FileAccess.ReadWrite;
                }
            }
        }
    }
}
