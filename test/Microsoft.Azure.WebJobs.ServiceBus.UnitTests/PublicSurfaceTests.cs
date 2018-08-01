// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class PublicSurfaceTests
    {
        [Fact]
        public void ServiceBusPublicSurface_LimitedToSpecificTypes()
        {
            var assembly = typeof(ServiceBusAttribute).Assembly;

            var expected = new[]
            {
                "EntityType",
                "MessageProcessor",
                "MessagingProvider",
                "ServiceBusAccountAttribute",
                "ServiceBusAttribute",
                "ServiceBusTriggerAttribute",
                "ServiceBusExtensionConfig",
                "ServiceBusHostBuilderExtensions",
                "ServiceBusOptions",
                "ServiceBusWebJobsStartup"
            };

            TestHelpers.AssertPublicTypes(expected, assembly);
        }
    }
}
