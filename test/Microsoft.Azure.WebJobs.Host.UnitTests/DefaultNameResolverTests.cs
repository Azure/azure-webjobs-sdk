// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class DefaultNameResolverTests
    {
        [Fact]
        public void Resolve_WithNullOrEmptyNameParameter_ReturnsNull()
        {
            var configuration = new ConfigurationBuilder().Build();
            var resolver = new DefaultNameResolver(configuration);

            // Assert we null returns null
            string result = resolver.Resolve(null);
            Assert.Null(result);

            // Assert empty returns null
            result = resolver.Resolve(string.Empty);
            Assert.Null(result);
        }
    }
}
