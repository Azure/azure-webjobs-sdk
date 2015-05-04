﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;

namespace Microsoft.Azure.WebJobs.Host.Queues.Bindings
{
    internal class ConverterValueBinder<TInput> : IOrderedValueBinder
    {
        private readonly IStorageQueue _queue;
        private readonly IConverter<TInput, IStorageQueueMessage> _converter;
        private readonly IMessageEnqueuedWatcher _messageEnqueuedWatcher;

        public ConverterValueBinder(IStorageQueue queue, IConverter<TInput, IStorageQueueMessage> converter,
            IMessageEnqueuedWatcher messageEnqueuedWatcher)
        {
            _queue = queue;
            _converter = converter;
            _messageEnqueuedWatcher = messageEnqueuedWatcher;
        }

        public BindStepOrder StepOrder
        {
            get { return BindStepOrder.Enqueue; }
        }

        public Type Type
        {
            get { return typeof(TInput); }
        }

        public object GetValue()
        {
            return default(TInput);
        }

        public string ToInvokeString()
        {
            return _queue.Name;
        }

        public async Task SetValueAsync(object value, CancellationToken cancellationToken)
        {
            IStorageQueueMessage message = _converter.Convert((TInput)value);
            Debug.Assert(message != null);
            await _queue.AddMessageAndCreateIfNotExistsAsync(message, cancellationToken);

            if (_messageEnqueuedWatcher != null)
            {
                _messageEnqueuedWatcher.Notify(_queue.Name);
            }
        }
    }
}
