// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.WebJobs.Host.Tables
{
    internal class TableEntityCollectorBinder<T> : IValueBinder
         where T : ITableEntity, new()
    {
        private readonly CloudTable _table;
        private readonly TableEntityCollectionAdapter<T> _value;
        private readonly Type _valueType;

        public TableEntityCollectorBinder(CloudTable table, TableEntityCollectionAdapter<T> value, Type valueType)
        {
            if (value != null && !valueType.IsAssignableFrom(value.GetType()))
            {
                throw new InvalidOperationException("value is not of the correct type.");
            }

            _table = table;
            _value = value;
            _valueType = valueType;
        }

        public Type Type
        {
            get { return _valueType; }
        }

        public object GetValue()
        {
            return _value;
        }

        public string ToInvokeString()
        {
            return _table.Name;
        }

        public async Task SetValueAsync(object value, CancellationToken cancellationToken)
        {
            await _value.FlushAllAsync();
        }
    }
}
