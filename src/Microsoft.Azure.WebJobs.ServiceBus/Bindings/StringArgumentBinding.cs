﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus.Bindings
{
    internal class StringArgumentBinding : IArgumentBinding<ServiceBusEntity>
    {
        public Type ValueType
        {
            get { return typeof(string); }
        }

        public Task<IValueProvider> BindAsync(ServiceBusEntity value, ValueBindingContext context)
        {
            IValueProvider provider = new StringValueBinder(value, context.FunctionInstanceId);
            return Task.FromResult(provider);
        }

        private class StringValueBinder : IOrderedValueBinder
        {
            private readonly ServiceBusEntity _entity;
            private readonly Guid _functionInstanceId;

            public StringValueBinder(ServiceBusEntity entity, Guid functionInstanceId)
            {
                _entity = entity;
                _functionInstanceId = functionInstanceId;
            }

            public int StepOrder
            {
                get { return BindStepOrders.Enqueue; }
            }

            public Type Type
            {
                get { return typeof(string); }
            }

            public object GetValue()
            {
                return null;
            }

            public string ToInvokeString()
            {
                return _entity.MessageSender.Path;
            }

            /// <summary>
            /// Creates and sends a BrokeredMessage with content provided in specified string.
            /// </summary>
            /// <param name="value">string as retrieved from user's WebJobs method argument.</param>
            /// <param name="cancellationToken">a cancellation token</param>
            /// <remarks>As this method handles out string parameter it distinguishes following possible scenarios:
            /// <item>
            /// <description>
            /// the value is null - no message will be sent;
            /// </description>
            /// </item>
            /// <item>
            /// <description>
            /// the value is an empty string - a message with empty content will be sent;
            /// </description>
            /// </item>
            /// <item>
            /// <description>
            /// the value is a non-empty string - a message with content from given argument will be sent.
            /// </description>
            /// </item>
            /// </remarks>
            public async Task SetValueAsync(object value, CancellationToken cancellationToken)
            {
                if (value == null)
                {
                    return;
                }

                string text = (string)value;
                byte[] bytes = StrictEncodings.Utf8.GetBytes(text);

                using (MemoryStream stream = new MemoryStream(bytes, writable: false))
                using (BrokeredMessage message = new BrokeredMessage(stream))
                {
                    await _entity.SendAndCreateQueueIfNotExistsAsync(message, _functionInstanceId, cancellationToken);
                }
            }
        }
    }
}
