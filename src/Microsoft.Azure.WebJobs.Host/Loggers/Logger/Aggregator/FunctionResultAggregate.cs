// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Logging
{
    internal class FunctionResultAggregate
    {
        public string Name { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public int Successes { get; set; }
        public int Failures { get; set; }
        public int Count => Successes + Failures;
        public double SuccessRate => Math.Round((Successes / (double)Count) * 100, 2);
    }
}
