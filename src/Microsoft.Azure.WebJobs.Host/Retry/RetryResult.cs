// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Represents retry settings.
    /// </summary>
    public class RetryResult : RetryAttribute
    {
        public RetryResult(int retryCount, string sleepDuration = null, bool exponentialBackoff = false)
            : base(retryCount, sleepDuration, exponentialBackoff)
        {
        }

        public string Format()
        {
            JObject options = new JObject
            {
                { nameof(RetryCount), RetryCount },
                { nameof(SleepDuration), SleepDuration },
                { nameof(ExponentialBackoff), ExponentialBackoff }
            };

            return options.ToString(Formatting.Indented);
        }
    }
}
