// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Core;
using Azure.Identity;
using System;
using System.Collections.Generic;
using System.Security.Authentication;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    public class TokenCredentialOptions
    {
        private const string AuthToken = "AAD";
        private const string AuthAuthorizationKey = "Authorization";
        private const string AuthClientIdKey = "ClientId";

        /// <summary>
        /// The client ID of an user-assigned identity.
        /// </summary>
        /// <remarks>
        /// This must be specified if you're using user-assigned managed identity.
        /// </remarks>
        public string ClientId { get; set; }

        /// <summary>
        /// Create an <see cref="TokenCredential"/> from an authentication string.
        /// </summary>
        /// <returns>New <see cref="ManagedIdentityCredential"/> from the authentication string.</returns>
        internal TokenCredential CreateTokenCredential()
        {
            return new ManagedIdentityCredential(ClientId);
        }

        /// <summary>
        /// Create an <see cref="TokenCredentialOptions"/> from an authentication string.
        /// </summary>
        /// <returns>New <see cref="TokenCredentialOptions"/> from the authentication string.</returns>
        public static TokenCredentialOptions ParseAuthenticationString(string applicationInsightsAuthenticationString)
        {
            if (string.IsNullOrWhiteSpace(applicationInsightsAuthenticationString))
            {
                throw new ArgumentNullException(nameof(applicationInsightsAuthenticationString), "Authentication string cannot be null or empty.");
            }

            var tokenCredentialOptions = new TokenCredentialOptions();
            bool isValidConfiguration = false;
            foreach ((int, int) split in Tokenize(applicationInsightsAuthenticationString))
            {
                (int start, int length) = split;

                // Trim whitespace from start
                while (length > 0 && char.IsWhiteSpace(applicationInsightsAuthenticationString[start]))
                {
                    start++;
                    length--;
                }

                // Ignore (allow) empty tokens.
                if (length == 0)
                {
                    continue;
                }

                // Find key-value separator.
                int indexOfEquals = applicationInsightsAuthenticationString.IndexOf('=', start, length);
                if (indexOfEquals < 0)
                {
                    continue;
                }

                // Extract key
                int keyLength = indexOfEquals - start;
                string key = applicationInsightsAuthenticationString.Substring(start, keyLength).TrimEnd();
                if (key.Length == 0)
                {
                    // Key is blank
                    continue;
                }

                if (key.Equals(AuthAuthorizationKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (!applicationInsightsAuthenticationString.Substring(indexOfEquals + 1, length - keyLength - 1).Trim().Equals(AuthToken, StringComparison.OrdinalIgnoreCase))
                    { 
                        throw new InvalidCredentialException("Credential supplied is not valid for the authorization mechanism being used in ApplicationInsights.");
                    }
                    isValidConfiguration = true;
                    continue;
                }
                if (key.Equals(AuthClientIdKey, StringComparison.OrdinalIgnoreCase))
                {
                    string clientId = applicationInsightsAuthenticationString.Substring(indexOfEquals + 1, length - keyLength - 1).Trim();
                    if (!Guid.TryParse(clientId, out Guid clientIdGuid))
                    {
                        throw new FormatException($"The Application Insights AuthenticationString {AuthClientIdKey} is not a valid GUID.");
                    }
                    tokenCredentialOptions.ClientId = clientId;
                    continue;
                }
            }     
            // Throw if the Authorization key is not present in the authentication string
            if (!isValidConfiguration)
            {
                throw new InvalidCredentialException("Authorization key is missing in the authentication string for ApplicationInsights.");
            }
            return tokenCredentialOptions;
        }

        private static IEnumerable<(int start, int length)> Tokenize(string value, char separator = ';')
        {
            for (int start = 0, end; start < value.Length; start = end + 1)
            {
                end = value.IndexOf(separator, start);
                if (end < 0)
                {
                    end = value.Length;
                }

                yield return (start, end - start);
            }
        }        
    }
}