// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class FilterBuilderTests
    {
        [Fact]
        public void Filter_MatchesLongestCategory()
        {

            var builder = new FilterBuilder();
            builder.DefaultLevel = LogLevel.Error;
            builder.CategoryFilters.Add("Microsoft", LogLevel.Critical);
            builder.CategoryFilters.Add("Microsoft.Azure", LogLevel.Error);
            builder.CategoryFilters.Add("Microsoft.Azure.WebJobs", LogLevel.Information);
            builder.CategoryFilters.Add("Microsoft.Azure.WebJobs.Host", LogLevel.Trace);

            Assert.False(builder.Filter("Microsoft", LogLevel.Information));
            Assert.False(builder.Filter("Microsoft.Azure", LogLevel.Information));
            Assert.False(builder.Filter("Microsoft.Azure.WebJob", LogLevel.Information));
            Assert.False(builder.Filter("NoMatch", LogLevel.Information));

            Assert.True(builder.Filter("Microsoft", LogLevel.Critical));
            Assert.True(builder.Filter("Microsoft.Azure", LogLevel.Critical));
            Assert.True(builder.Filter("Microsoft.Azure.WebJobs.Extensions", LogLevel.Information));
            Assert.True(builder.Filter("Microsoft.Azure.WebJobs.Host", LogLevel.Debug));
            Assert.True(builder.Filter("NoMatch", LogLevel.Error));
        }
    }
}
