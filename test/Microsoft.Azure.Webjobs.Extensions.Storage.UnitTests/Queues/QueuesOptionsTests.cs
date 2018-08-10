﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class QueuesOptionsTests
    {
        [Fact]
        public void Constructor_Defaults()
        {
            QueuesOptions options = new QueuesOptions();

            Assert.Equal(16, options.BatchSize);
            Assert.Equal(8, options.NewBatchThreshold);
            Assert.Equal(QueuePollingIntervals.DefaultMaximum, options.MaxPollingInterval);
        }

        [Fact]
        public void NewBatchThreshold_CanSetAndGetValue()
        {
            QueuesOptions options = new QueuesOptions();

            // Unless explicitly set, NewBatchThreshold will be computed based
            // on the current BatchSize
            options.BatchSize = 20;
            Assert.Equal(10, options.NewBatchThreshold);
            options.BatchSize = 32;
            Assert.Equal(16, options.NewBatchThreshold);

            // Once set, the set value holds
            options.NewBatchThreshold = 1000;
            Assert.Equal(1000, options.NewBatchThreshold);
            options.BatchSize = 8;
            Assert.Equal(1000, options.NewBatchThreshold);
        }

        [Fact]
        public void VisibilityTimeout_CanGetAndSetValue()
        {
            QueuesOptions options = new QueuesOptions();

            Assert.Equal(TimeSpan.Zero, options.VisibilityTimeout);

            options.VisibilityTimeout = TimeSpan.FromSeconds(30);
            Assert.Equal(TimeSpan.FromSeconds(30), options.VisibilityTimeout);
        }

        [Fact]
        public void JsonSerialization()
        {
            var jo = new JObject
            {
                { "MaxPollingInterval", 5000 }
            };
            var options = jo.ToObject<QueuesOptions>();
            Assert.Equal(TimeSpan.FromMilliseconds(5000), options.MaxPollingInterval);
            string json = JsonConvert.SerializeObject(options);

            jo = new JObject
            {
                { "MaxPollingInterval", "00:00:05" }
            };
            options = jo.ToObject<QueuesOptions>();
            Assert.Equal(TimeSpan.FromMilliseconds(5000), options.MaxPollingInterval);
            json = JsonConvert.SerializeObject(options);
        }
    }
}
