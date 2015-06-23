﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host.Queues
{
    internal interface IQueueConfiguration
    {
        int BatchSize { get; }

        int NewBatchThreshold { get; }

        TimeSpan MaxPollingInterval { get; }

        int MaxDequeueCount { get; }

        IQueueProcessorFactory QueueProcessorFactory { get; }
    }
}
