// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class RetryAttributeTests
    {
        [Fact]
        public void Constructor_Expected()
        {
            var retry = new RetryAttribute(5, "01:02:25");
            Assert.Equal(5, retry.RetryCount);
            Assert.Equal(25, retry.SleepDuration.Seconds);
            Assert.Equal(2, retry.SleepDuration.Minutes);
            Assert.Equal(1, retry.SleepDuration.Hours);
            Assert.False(retry.ExponentialBackoff);

            retry = new RetryAttribute(5, "01:02:25", true);
            Assert.Equal(5, retry.RetryCount);
            Assert.Equal(25, retry.SleepDuration.Seconds);
            Assert.Equal(2, retry.SleepDuration.Minutes);
            Assert.Equal(1, retry.SleepDuration.Hours);
            Assert.True(retry.ExponentialBackoff);

            retry = new RetryAttribute(-1, null, true);
            Assert.Equal(-1, retry.RetryCount);
            Assert.True(retry.ExponentialBackoff);
        }

        [Fact]
        public void Constructor_ThrowsException()
        {
            RetryAttribute retry = null;
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => retry = new RetryAttribute(-2, null));
            Assert.Equal("'retryCount' must be >= -1.", ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => retry = new RetryAttribute(-1, "test"));
            Assert.Equal("Can't parse sleepDuration='test', please the string in format '00:00:00'.", ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => retry = new RetryAttribute(-1, null));
            Assert.Equal("Can't parse sleepDuration='', please the string in format '00:00:00'.", ex.Message);
        }
    }
}
