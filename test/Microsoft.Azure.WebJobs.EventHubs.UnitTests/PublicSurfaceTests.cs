// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class PublicSurfaceTests
    {
        [Fact]
        public void EventHubsPublicSurface_LimitedToSpecificTypes()
        {
            var assembly = typeof(EventHubAttribute).Assembly;

            var expected = new[]
            {
                "EventHubAttribute",
                "EventHubTriggerAttribute",
                "EventHubConfiguration",
                "EventHubExtensionConfigProvider",
                "EventHubWebJobsBuilderExtensions",
                "EventHubsWebJobsStartup"
            };

            TestHelpers.AssertPublicTypes(expected, assembly);
        }
    }
}
