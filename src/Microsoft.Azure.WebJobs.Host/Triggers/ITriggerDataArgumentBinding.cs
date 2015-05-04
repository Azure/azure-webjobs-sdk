﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Host.Triggers
{
    public interface ITriggerDataArgumentBinding<TTriggerValue>
    {
        Type ValueType { get; }

        IReadOnlyDictionary<string, Type> BindingDataContract { get; }

        Task<ITriggerData> BindAsync(TTriggerValue value, ValueBindingContext context);
    }

}
