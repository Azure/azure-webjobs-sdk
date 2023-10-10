// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using SampleHost.Filters;

namespace SampleHost
{
    public class Functions
    {
        public void ProcessWorkItem([QueueTrigger("atestqueue")] string message, ILogger logger)
        {
            logger.LogInformation($"Processed message {message}");
        }
    }
}
