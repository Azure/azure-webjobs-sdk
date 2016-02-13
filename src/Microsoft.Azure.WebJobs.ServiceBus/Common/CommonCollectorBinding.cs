// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.ServiceBus.Messaging;
using Microsoft.Azure.WebJobs.Host.Protocols;
using System.Globalization;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // This binding is static per parameter, and then called on each invocation 
    // to produce a new parameter instance. 
    internal class CommonCollectorBinding<TMessage, TContext> : IBinding
    {
        private readonly TContext _client;
        private readonly ParameterDescriptor _param;
        private readonly Func<TContext, ValueBindingContext, IValueProvider> _argumentBuilder;
        private readonly Func<string, TContext> _invokeStringBinder;

        public CommonCollectorBinding(
            TContext client,
            Func<TContext, ValueBindingContext, IValueProvider> argumentBuilder,
            ParameterDescriptor param,
            Func<string, TContext> invokeStringBinder
            )
        {
            this._client = client;
            this._argumentBuilder = argumentBuilder;
            this._param = param;
            this._invokeStringBinder = invokeStringBinder;
        }

        public bool FromAttribute
        {
            get
            {
                return true;
            }
        }

        public Task<IValueProvider> BindAsync(BindingContext context)
        {
            return BindAsync(null, context.ValueContext);
        }

        public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            TContext client = this._client;

            string invokeString = value as string;
            if (invokeString != null)
            {
                client = this._invokeStringBinder(invokeString);
            }

            IValueProvider valueProvider = _argumentBuilder(_client, context);
            return Task.FromResult(valueProvider);
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return _param;
        }
    }
}