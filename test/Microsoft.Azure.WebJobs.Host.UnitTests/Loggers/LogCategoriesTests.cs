// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class LogCategoriesTests
    {
        [Theory]
        [InlineData("Function.Function1", true)]
        [InlineData("Function.Function_1", true)]
        [InlineData("Function.Function-1", true)]
        [InlineData("Function.Function1.User", false)]
        [InlineData("Function.Function1.SomethingElse", false)]
        [InlineData("Function.Function 1", false)]
        [InlineData("function.Function1", false)]        
        public void IsFunctionCategory(string category, bool expected)
        {
            Assert.True(LogCategories.IsFunctionCategory(category) == expected, $"Category: {category}; Expected: {expected}");
        }

        [Theory]
        [InlineData("Function.Function1.User", true)]
        [InlineData("Function.Function_1.User", true)]
        [InlineData("Function.Function-1.User", true)]
        [InlineData("Function.Function1", false)]
        [InlineData("Function.Function1.SomethingElse.User", false)]
        [InlineData("Function.Function1.User.SomethingElse", false)]
        [InlineData("function.Function1.user", false)]
        public void IsFunctionUserCategory(string category, bool expected)
        {
            Assert.True(LogCategories.IsFunctionUserCategory(category) == expected, $"Category: {category}; Expected: {expected}");
        }
    }
}
