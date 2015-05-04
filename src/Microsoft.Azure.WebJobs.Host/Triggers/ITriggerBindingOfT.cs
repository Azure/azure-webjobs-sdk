﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Host.Triggers
{
    /// <summary>
    /// Interface defining a trigger parameter binding.
    /// </summary>
    /// <typeparam name="TTriggerValue">The trigger value type that this binding binds to.</typeparam>
    public interface ITriggerBinding<TTriggerValue> : ITriggerBinding
    {
        /// <summary>
        /// Perform a bind to the specified trigger value using the specified binding context.
        /// </summary>
        /// <param name="value">The trigger value to bind to.</param>
        /// <param name="context">The binding context.</param>
        /// <returns>A task that returns the <see cref="ITriggerData"/> for the binding.</returns>
        Task<ITriggerData> BindAsync(TTriggerValue value, ValueBindingContext context);
    }
}
