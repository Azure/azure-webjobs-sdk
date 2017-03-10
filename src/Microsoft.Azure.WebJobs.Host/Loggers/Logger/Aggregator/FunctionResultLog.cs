// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal class FunctionResultLog
    {
        public string Name { get; set; }

        public DateTimeOffset StartTime { get; set; }

        public DateTimeOffset EndTime { get; set; }

        public int DurationInMilliseconds { get; set; }

        public bool Success { get; set; }
    }
}
