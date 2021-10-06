// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.Host.Storage
{
    public class StorageServiceUriOptions
    {
        private const string DefaultProtocol = "https";
        private const string DefaultEndpointSuffix = "core.windows.net";

        public string BlobServiceUri { get; set; }

        public string AccountName { get; set; }

        public Uri GetServiceUri(string serviceUriSubDomain = "blob")
        {
            if (!string.IsNullOrEmpty(BlobServiceUri))
            {
                return new Uri(BlobServiceUri);
            }
            else if (!string.IsNullOrEmpty(AccountName))
            {
                var uri = string.Format(CultureInfo.InvariantCulture, "{0}://{1}.{2}.{3}", DefaultProtocol, AccountName, serviceUriSubDomain, DefaultEndpointSuffix);
                return new Uri(uri);
            }

            return default;
        }
    }
}
