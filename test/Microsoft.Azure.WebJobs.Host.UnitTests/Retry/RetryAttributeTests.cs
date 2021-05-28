// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class RetryAttributeTests
    {
        [Fact]
        public void FixedDelay_Constructor_Expected()
        {
            var retry = new FixedDelayRetryAttribute(5, "01:02:25");
            Assert.Equal(5, retry.MaxRetryCount);
            Assert.Equal("01:02:25", retry.DelayInterval);

            retry = new FixedDelayRetryAttribute(-1, "00:00:10");
            Assert.Equal(-1, retry.MaxRetryCount);
            Assert.Equal("00:00:10", retry.DelayInterval);
        }

        [Fact]
        public void FixedDelay_Constructor_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedDelayRetryAttribute(5, "invalid"));
        }

        [Fact]
        public void FixedDelay_NextDelay_ReturnsExpected()
        {
            var retry = new FixedDelayRetryAttribute(5, "01:00:25");
            RetryContext retryContext = new RetryContext
            {
                 RetryCount = 3
            };
            Assert.Equal(TimeSpan.Parse("01:00:25"), retry.GetNextDelay(retryContext));

            RetryContext lastRetryContext = new RetryContext
            {
                RetryCount = 6
            };
            Assert.Equal(TimeSpan.Zero, retry.GetNextDelay(lastRetryContext));
        }

        [Fact]
        public void ExponentialBackOffDelay_Constructor_Expected()
        {
            var retry = new ExponentialBackoffRetryAttribute(5, "01:02:25", "02:00:10");
            Assert.Equal(5, retry.MaxRetryCount);
            Assert.Equal("01:02:25", retry.MinimumInterval);
            Assert.Equal("02:00:10", retry.MaximumInterval);

            retry = new ExponentialBackoffRetryAttribute(-1, "00:00:10", "00:00:30");
            Assert.Equal(-1, retry.MaxRetryCount);
            Assert.Equal("00:00:10", retry.MinimumInterval);
            Assert.Equal("00:00:30", retry.MaximumInterval);
        }

        [Fact]
        public void ExponentialBackOffDelay_Constructor_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialBackoffRetryAttribute(5, "01:020000000000:25", "00:00:10"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialBackoffRetryAttribute(5, "01:02:25", "100000000"));
            Assert.Throws<ArgumentException>(() => new ExponentialBackoffRetryAttribute(5, "05:02:25", "00:00:10"));
        }

        [Fact]
        public void ExponentialBackOffDelay_NextDelay_ReturnsExpected()
        {
            var retry = new ExponentialBackoffRetryAttribute(5, "01:02:25", "02:00:10");
            RetryContext retryContext = new RetryContext
            {
                RetryCount = 1
            };
            Assert.Equal(TimeSpan.Parse(retry.MinimumInterval), retry.GetNextDelay(retryContext));

            RetryContext lastRetryContext = new RetryContext
            {
                RetryCount = 6
            };
            Assert.Equal(TimeSpan.Zero, retry.GetNextDelay(lastRetryContext));
        }

        [Fact]
        public void ExponentialBackOffDelay_GetNextDelay_Is_Stateless()
        {
            var retry = new ExponentialBackoffRetryAttribute(5, "01:02:25", "02:00:10");
            RetryContext retryContext1 = new RetryContext
            {
                RetryCount = 1
            };
            RetryContext retryContext2 = new RetryContext
            {
                RetryCount = 1
            };
            Assert.Equal(retry.GetNextDelay(retryContext1), retry.GetNextDelay(retryContext2));
        }

        [Fact]
        public void ExponentialBackOffDelay_GetNextDelay_Is_Increased()
        {
            var retry = new ExponentialBackoffRetryAttribute(5, "01:02:25", "02:00:10");
            RetryContext retryContext = new RetryContext
            {
                RetryCount = 1
            };
            var delay1 = retry.GetNextDelay(retryContext);
            var delay2 = retry.GetNextDelay(retryContext);
            Assert.True(delay2 > delay1);
        }
    }
}
