// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Tables
{
    public class TableFilterParameterResolverTests
    {
        [Theory]
        [InlineData("Age gt {age}", "age", 7, "25 or true")]
        [InlineData("{age} lt Age", "age", 0, "25 or true")]
        public void TryResolve_InvalidIntegerFilter_Throws(string template, string paramName, int paramIndex, string value)
        {
            var resolver = new TableFilterParameterResolver(new DefaultParameterResolver());

            var context = new ParameterResolverContext
            {
                Template = template,
                ParameterName = paramName,
                ParameterIndex = paramIndex,
                BindingData = new Dictionary<string, string>
                {
                    { paramName, value }
                }
            };
            var ex = Assert.Throws<InvalidOperationException>(() => resolver.TryResolve(context));
            Assert.Equal($"An invalid parameter value was specified for filter parameter '{paramName}'", ex.Message);
        }

        [Theory]
        [InlineData("Name eq '{name}'", "name", 9, "x' or 'x' eq 'x")]
        [InlineData("'{name}' eq Name", "name", 1, "x' or 'x' eq 'x")]
        public void TryResolve_InvalidStringFilter_Throws(string template, string paramName, int paramIndex, string value)
        {
            var resolver = new TableFilterParameterResolver(new DefaultParameterResolver());

            var context = new ParameterResolverContext
            {
                Template = template,
                ParameterName = paramName,
                ParameterIndex = paramIndex,
                BindingData = new Dictionary<string, string>
                {
                    { paramName, value }
                }
            };
            var ex = Assert.Throws<InvalidOperationException>(() => resolver.TryResolve(context));
            Assert.Equal($"An invalid parameter value was specified for filter parameter '{paramName}'", ex.Message);
        }

        [Theory]
        [InlineData("Age gt {age}", "age", 7, "25")]
        [InlineData("{age} lt Age", "age", 0, "25")]
        public void TryResolve_ValidIntegerFilter_Succeeds(string template, string paramName, int paramIndex, string value)
        {
            var resolver = new TableFilterParameterResolver(new DefaultParameterResolver());

            var context = new ParameterResolverContext
            {
                Template = template,
                ParameterName = paramName,
                ParameterIndex = paramIndex,
                BindingData = new Dictionary<string, string>
                {
                    { paramName, value }
                }
            };
            var result = resolver.TryResolve(context);
            Assert.True(result);
        }

        [Theory]
        [InlineData("Name eq '{name}'", "name", 9, "Mark O''Malley")]
        [InlineData("'{name}' eq Name", "name", 1, "Mark O''Malley")]
        [InlineData("Name eq '{name}'", "name", 9, "Mark Smith")]
        [InlineData("Name eq '{name}'", "name", 9, "''''''''")]
        public void TryResolve_ValidStringFilter_Succeeds(string template, string paramName, int paramIndex, string value)
        {
            var resolver = new TableFilterParameterResolver(new DefaultParameterResolver());

            var context = new ParameterResolverContext
            {
                Template = template,
                ParameterName = paramName,
                ParameterIndex = paramIndex,
                BindingData = new Dictionary<string, string>
                {
                    { paramName, value }
                }
            };
            var result = resolver.TryResolve(context);
            Assert.True(result);
        }

        [Theory]
        [InlineData("x' or 'x' eq 'x", true)]
        [InlineData("x '", true)]
        [InlineData("'", true)]
        [InlineData("'''", true)]
        [InlineData("", false)]
        [InlineData("Mark O''Malley", false)]
        [InlineData("x''y''''z''''", false)]
        [InlineData("''", false)]
        [InlineData("''''''''", false)]
        public void ContainsUnescapedQuotes_ReturnsExpectedValue(string value, bool expected)
        {
            bool result = TableFilterParameterResolver.ContainsUnescapedSingleQuotes(value);
            Assert.Equal(expected, result);
        }
    }
}
