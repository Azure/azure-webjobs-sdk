// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    internal class CacheAwareValueProvider : IValueProvider, IValueBinder
    {
        private object _value;
        private string _invokeString;

        public CacheAwareValueProvider(object value, Type type, string invokeString)
        {
            this._value = value;
            this.Type = type;
            this._invokeString = invokeString;
        }

        public Type Type { get; set; }

        public Task<object> GetValueAsync()
        {
            return Task.FromResult(_value);
        }

        public async Task SetValueAsync(object value, CancellationToken cancellationToken)
        {
            if (this._value is ICacheAwareWriteObject writeObj)
            {
                await writeObj.TryPutToCacheAsync(isDeleteOnFailure: true);
            }

            if (this._value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public string ToInvokeString()
        {
            return _invokeString;
        }
    }
}
