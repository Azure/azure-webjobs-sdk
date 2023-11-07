// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Core;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights.Tests
{
    public class TokenCredentialOptionsTests
    {
        [Fact]
        public void TestCreateTokenCredential()
        {
            var options = new TokenCredentialOptions("Authorization=AAD;ClientId=ClientIdValue");
            TokenCredential credential = options.CreateTokenCredential();
            Assert.NotNull(credential);
        }

        [Fact]
        public void TestParseAuthenticationString()
        {
            var options = new TokenCredentialOptions("Authorization=AAD;ClientId=ClientIdValue");
            Assert.Equal("AAD", options.Authorization);
            Assert.Equal("ClientIdValue", options.ClientId);
        }

        [Fact]
        public void TestParseAuthenticationStringWithEmptyValue()
        {
            var options = new TokenCredentialOptions("");
            Assert.Null(options.Authorization);
            Assert.Null(options.ClientId);
        }

        [Fact]
        public void TestParseAuthenticationStringWithEmptyToken()
        {
            var options = new TokenCredentialOptions("Authorization=AAD;;ClientId=ClientIdValue");
            Assert.Equal("AAD", options.Authorization);
            Assert.Equal("ClientIdValue", options.ClientId);
        }

        [Fact]
        public void TestParseAuthenticationStringWithDuplicateKeys()
        {
            var options = new TokenCredentialOptions("Authorization=AAD1;Authorization=AAD2");
            Assert.Null(options.ClientId);
            Assert.Equal("AAD2", options.Authorization);
        }

        [Fact]
        public void TestParseAuthenticationStringWithCase()
        {
            var options = new TokenCredentialOptions("authoRization=AAD ;; ClIentId = ClientIdValue ");
            Assert.Equal("AAD", options.Authorization);
            Assert.Equal("ClientIdValue", options.ClientId);
        }

        [Fact]
        public void TestParseAuthenticationStringWithWhiteSpace()
        {
            var options = new TokenCredentialOptions("Authorization=AAD; ClientId=ClientIdValue ");
            Assert.Equal("AAD", options.Authorization);
            Assert.Equal("ClientIdValue", options.ClientId);
        }
    }
}