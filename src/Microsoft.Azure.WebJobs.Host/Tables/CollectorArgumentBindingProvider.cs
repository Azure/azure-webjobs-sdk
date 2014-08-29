// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.WebJobs.Host.Tables
{
    internal class CollectorArgumentBindingProvider : ITableArgumentBindingProvider
    {
        public IArgumentBinding<CloudTable> TryCreate(Type parameterType)
        {
            if (!parameterType.IsGenericType || parameterType.GetGenericTypeDefinition() != typeof(ICollector<>))
            {
                return null;
            }

            Type entityType = GetCollectorItemType(parameterType);
            TableClient.VerifyDefaultConstructor(entityType);

            return CreateBinding(entityType);
        }

        private static Type GetCollectorItemType(Type queryableType)
        {
            Type[] genericArguments = queryableType.GetGenericArguments();
            var itemType = genericArguments[0];
            return itemType;
        }

        private static IArgumentBinding<CloudTable> CreateBinding(Type entityType)
        {
            if (TableClient.ImplementsITableEntity(entityType))
            {
                Type genericType = typeof(CollectorTableEntityArgumentBinding<>).MakeGenericType(entityType);
                return (IArgumentBinding<CloudTable>)Activator.CreateInstance(genericType);
            }
            else
            {
                Type genericType = typeof(CollectorPocoEntityArgumentBinding<>).MakeGenericType(entityType);
                return (IArgumentBinding<CloudTable>)Activator.CreateInstance(genericType);
            }
        }

        private class CollectorTableEntityArgumentBinding<TElement> : IArgumentBinding<CloudTable>
            where TElement : ITableEntity, new()
        {
            public Type ValueType
            {
                get { return typeof(ICollector<TElement>); }
            }

            public async Task<IValueProvider> BindAsync(CloudTable value, ValueBindingContext context)
            {
                await value.CreateIfNotExistsAsync();
                TableEntityCollectionAdapter<TElement> collector = new TableEntityCollectionAdapter<TElement>(value);
                return new TableEntityBatchValuesBinder<TElement>(value, collector, typeof(ICollector<TElement>));
            }
        }

        private class CollectorPocoEntityArgumentBinding<TElement> : IArgumentBinding<CloudTable>
        {
            public Type ValueType
            {
                get { return typeof(ICollector<TElement>); }
            }

            public async Task<IValueProvider> BindAsync(CloudTable value, ValueBindingContext context)
            {
                await value.CreateIfNotExistsAsync();
                PocoEntityCollectionAdapter<TElement> collector = new PocoEntityCollectionAdapter<TElement>(value);
                return new PocoEntityBatchValuesBinder<TElement>(value, collector, typeof(ICollector<TElement>));
            }
        }
    }
}
