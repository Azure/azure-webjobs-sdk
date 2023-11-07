// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Core;
using Azure.Identity;
using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    public class TokenCredentialOptions
    {
        private const string AuthToken = "AAD";
        private const string AuthAuthorizationKey = "Authorization";
        private const string AuthClientIdKey = "ClientId";

        public TokenCredentialOptions(string authenticationString)
        {
            if (!string.IsNullOrWhiteSpace(authenticationString))
            {
                var tokens = ParseAuthenticationString(authenticationString);

                if (tokens.ContainsKey(AuthAuthorizationKey))
                {
                    Authorization = tokens[AuthAuthorizationKey];
                }
                if (tokens.ContainsKey(AuthClientIdKey))
                {
                    ClientId = tokens[AuthClientIdKey];
                }
            }
        }
        /// <summary>
        /// The authentication kind.
        /// </summary>
        public string Authorization { get; set; }

        /// <summary>
        /// The client ID of an user-assigned identity.
        /// </summary>
        /// <remarks>
        /// This must be specified if you're using user-assigned managed
        /// identity.
        /// </remarks>
        public string ClientId { get; set; }

        /// <summary>
        /// Create an <see cref="TokenCredential"/> from an authentication string.
        /// </summary>
        /// <param name="value">
        /// The authentication string containing semi-colon separated key=value tokens.</param>
        /// <returns>New <see cref="ManagedIdentityCredential"/> from the authentication string.</returns>
        public TokenCredential CreateTokenCredential()
        {
            switch (Authorization)
            {
                case AuthToken when string.IsNullOrWhiteSpace(ClientId):
                    return new ManagedIdentityCredential();
                case AuthToken when !string.IsNullOrWhiteSpace(ClientId):
                    return new ManagedIdentityCredential(ClientId);
                default:
                    return null;
            }
        }

        private IReadOnlyDictionary<string, string> ParseAuthenticationString(string value)
        {            
            var tokens = new Dictionary<string, string>(2, StringComparer.OrdinalIgnoreCase);            

            foreach ((int, int) split in Tokenize(value))
            {
                (int start, int length) = split;

                // Trim whitespace from start
                while (length > 0 && char.IsWhiteSpace(value[start]))
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
                int indexOfEquals = value.IndexOf('=', start, length);
                if (indexOfEquals < 0)
                {
                    continue;
                }

                // Extract key
                int keyLength = indexOfEquals - start;
                string key = value.Substring(start, keyLength).TrimEnd();
                if (key.Length == 0)
                {
                    // Key is blank
                    continue;
                }

                // Add token and allow for duplicate keys
                tokens[key] = value.Substring(indexOfEquals + 1, length - keyLength - 1).Trim();
            }
            return tokens;
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
