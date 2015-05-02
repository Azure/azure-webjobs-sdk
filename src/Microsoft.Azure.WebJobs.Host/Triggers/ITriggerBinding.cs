// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.Triggers
{
    public interface ITriggerBinding
    {
        IReadOnlyDictionary<string, Type> BindingDataContract { get; }

        Task<ITriggerData> BindAsync(object value, ValueBindingContext context);

        IListenerFactory CreateListenerFactory(ITriggeredFunctionExecutor executor);

        ParameterDescriptor ToParameterDescriptor();
    }
}
