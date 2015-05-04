﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Host.Queues
{
    internal interface IBindableQueuePath : IBindablePath<string>
    {
        string QueueNamePattern { get; }
    }
}
