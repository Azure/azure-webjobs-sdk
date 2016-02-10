// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Bindings;
using System;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // $$$ How did SerivceBus avoid this? SB counterpart is BrokeredMessageValueProvider 
    internal class ConstantValueProvider : IValueProvider
    {
        internal object _value;

        public Type Type { get; set; }

        public object GetValue()
        {
            return _value;
        }

        public string ToInvokeString()
        {
            return "na"; // $$$
        }
    }
}