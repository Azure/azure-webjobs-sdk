﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Queues
{
    public class ParameterizedQueuePathTests
    {
        [Fact]
        public void Bind_IfNotNullBindingData_ReturnsResolvedQueueName()
        {
            const string queueNamePattern = "queue-{name}-with-{parameter}";
            var bindingData = new Dictionary<string, object> { { "name", "name" }, { "parameter", "parameter" } };
            IBindableQueuePath path = CreateProductUnderTest(queueNamePattern);

            string result = path.Bind(bindingData);

            Assert.Equal("queue-name-with-parameter", result);
        }

        [Fact]
        public void Bind_IfNullBindingData_Throws()
        {
            const string queueNamePattern = "queue-{name}-with-{parameter}";
            IBindableQueuePath path = CreateProductUnderTest(queueNamePattern);

            ExceptionAssert.ThrowsArgumentNull(() => path.Bind(null), "bindingData");
        }

        private static IBindableQueuePath CreateProductUnderTest(string queueNamePattern)
        {
            List<string> parameterNames = new List<string>();
            BindingDataPath.AddParameterNames(queueNamePattern, parameterNames);
            IBindableQueuePath path = new ParameterizedQueuePath(queueNamePattern, parameterNames);
            return path;
        }
    }
}
