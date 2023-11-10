// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Core;
using System;
using System.Collections.Generic;
using System.Security.Authentication;
using Xunit;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights.Tests
{
    public class TokenCredentialOptionsTests
    {
        [Fact]
        public void TestCreateTokenCredential()
        {
            var options = TokenCredentialOptions.ParseAuthenticationString($"Authorization=AAD;ClientId={Guid.NewGuid()}");
            var credential = options.CreateTokenCredential();
            Assert.NotNull(credential);
        }

        [Fact]
        public void TestParseAuthenticationString()
        {
            var clientId = Guid.NewGuid().ToString();
            var options = TokenCredentialOptions.ParseAuthenticationString($"Authorization=AAD;ClientId={clientId}");
            Assert.Equal(clientId, options.ClientId);
        }

        [Fact]
        public void TestParseAuthenticationStringWithEmptyValue()
        {
            var clientId = Guid.NewGuid().ToString();
            ArgumentNullException argumentNullException = Assert.Throws<ArgumentNullException>(()=> TokenCredentialOptions.ParseAuthenticationString(""));
            Assert.Equal("applicationInsightsAuthenticationString", argumentNullException.ParamName);
        }

        [Fact]
        public void TestParseAuthenticationStringWithInvalidAuthorization()
        {
            var clientId = Guid.NewGuid().ToString();
            InvalidCredentialException argumentNullException = Assert.Throws<InvalidCredentialException>(() => TokenCredentialOptions.ParseAuthenticationString($"Authorization=AAD1"));
            Assert.Equal("Credential supplied is not valid for the authorization mechanism being used in ApplicationInsights.", argumentNullException.Message);
        }

        [Fact]
        public void TestParseAuthenticationStringWithNoAuthorization()
        {
            var clientId = Guid.NewGuid().ToString();
            InvalidCredentialException argumentNullException = Assert.Throws<InvalidCredentialException>(() => TokenCredentialOptions.ParseAuthenticationString($"Auth123=AAD"));
            Assert.Equal("Authorization key is missing in the authentication string for ApplicationInsights.", argumentNullException.Message);
        }

        [Fact]
        public void TestParseAuthenticationStringWithInvalidClientId()
        {
            var clientId = Guid.NewGuid().ToString();
            FormatException argumentNullException = Assert.ThrowsAny<FormatException>(() => TokenCredentialOptions.ParseAuthenticationString($"Authorization=AAD;ClientId=123"));
            Assert.Equal("The Application Insights AuthenticationString ClientId is not a valid GUID.", argumentNullException.Message);
        }

        [Fact]
        public void TestParseAuthenticationStringWithEmptyToken()
        {
            var clientId = Guid.NewGuid().ToString();
            var options = TokenCredentialOptions.ParseAuthenticationString($"Authorization=AAD;;ClientId={clientId}");
            Assert.Equal(clientId, options.ClientId);
        }

        [Fact]
        public void TestParseAuthenticationStringWithDuplicateKeys()
        {
            var options = TokenCredentialOptions.ParseAuthenticationString("Authorization=AAD;Authorization=AAD;");
            Assert.Null(options.ClientId);
        }

        [Fact]
        public void TestParseAuthenticationStringWithCase()
        {
            var clientId = Guid.NewGuid().ToString();
            var options = TokenCredentialOptions.ParseAuthenticationString($"authoRization=AAD ;; ClIentId = {clientId} ");
            Assert.Equal(clientId, options.ClientId);
        }

        [Fact]
        public void TestParseAuthenticationStringWithWhiteSpace()
        {
            var clientId = Guid.NewGuid().ToString();
            var options = TokenCredentialOptions.ParseAuthenticationString($"Authorization=AAD; ClientId={clientId} ");            
            Assert.Equal(clientId, options.ClientId);
        }
    }
}