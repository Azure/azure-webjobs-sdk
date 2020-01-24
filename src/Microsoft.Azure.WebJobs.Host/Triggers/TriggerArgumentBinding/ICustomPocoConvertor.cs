// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host.Triggers
{

    /// <summary>
    /// Adds custom conversion for PocoTriggerArgumentBindings which does not use json format.
    /// </summary>
    /// <typeparam name="TMessage">The native message type. For ServiceBus, this would be Message.</typeparam>
    public interface ICustomPocoConvertor<TMessage>
    {
        /// <summary>
        /// Given message and type to object.
        /// This is used if we want convertor message to POCO using other than json deserialization
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        object Convert(TMessage message, Type type);
    }
}
