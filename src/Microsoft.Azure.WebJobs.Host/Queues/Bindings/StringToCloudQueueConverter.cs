﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Microsoft.Azure.WebJobs.Host.Queues.Bindings
{
    internal class StringToCloudQueueConverter : IConverter<string, CloudQueue>
    {
        private readonly CloudQueueClient _client;
        private readonly IBindableQueuePath _defaultPath;

        public StringToCloudQueueConverter(CloudQueueClient client, IBindableQueuePath defaultPath)
        {
            _client = client;
            _defaultPath = defaultPath;
        }

        public CloudQueue Convert(string input)
        {
            string queueName;

            // For convenience, treat an an empty string as a request for the default value.
            if (String.IsNullOrEmpty(input) && _defaultPath.IsBound)
            {
                queueName = _defaultPath.Bind(null);
            }
            else
            {
                queueName = BindableQueuePath.NormalizeAndValidate(input);
            }

            return _client.GetQueueReference(queueName);
        }
    }
}
