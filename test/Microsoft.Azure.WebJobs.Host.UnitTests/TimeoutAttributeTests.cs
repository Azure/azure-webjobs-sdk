// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class TimeoutAttributeTests
    {
        [Fact]
        public void Constructor_DefaultsProperties()
        {
            var timeout = new TimeoutAttribute("00:00:25");
            Assert.Equal(TimeSpan.FromSeconds(25), timeout.Timeout);
            Assert.Equal(TimeSpan.FromSeconds(2), timeout.GracePeriod);
            Assert.False(timeout.TimeoutWhileDebugging);
            Assert.False(timeout.ThrowOnTimeout);

            timeout = new TimeoutAttribute("00:05:00", "00:00:30");
            Assert.Equal(TimeSpan.FromMinutes(5), timeout.Timeout);
            Assert.Equal(TimeSpan.FromSeconds(30), timeout.GracePeriod);
            Assert.False(timeout.TimeoutWhileDebugging);
            Assert.False(timeout.ThrowOnTimeout);
        }
    }
}
