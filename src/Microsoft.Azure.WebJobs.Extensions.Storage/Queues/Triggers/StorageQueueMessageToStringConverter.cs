﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Microsoft.Azure.WebJobs.Host.Queues.Triggers
{
    internal class StorageQueueMessageToStringConverter : IConverter<CloudQueueMessage, string>
    {
        public string Convert(CloudQueueMessage input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            return input.AsString;
        }
    }
}
