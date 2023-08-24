// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// Type was moved from https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script/Host/PrimaryHostCoordinatorOptions.cs

using System;

namespace Microsoft.Azure.WebJobs.Hosting
{
    public class PrimaryHostCoordinatorOptions
    {
        private TimeSpan _leaseTimeout = TimeSpan.FromSeconds(15);

        public PrimaryHostCoordinatorOptions()
        {
            Enabled = false;
        }

        public bool Enabled { get; set; }

        public TimeSpan LeaseTimeout
        {
            get
            {
                return _leaseTimeout;
            }

            set
            {
                if (value < TimeSpan.FromSeconds(15) || value > TimeSpan.FromSeconds(60))
                {
                    throw new ArgumentOutOfRangeException(nameof(LeaseTimeout), $"The {nameof(LeaseTimeout)} should be between 15 and 60 seconds but was '{value}'");
                }

                _leaseTimeout = value;
            }
        }

        public TimeSpan? RenewalInterval { get; set; } = null;
    }
}
