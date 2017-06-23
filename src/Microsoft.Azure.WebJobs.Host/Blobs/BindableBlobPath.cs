﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;

namespace Microsoft.Azure.WebJobs.Host.Blobs
{
    internal static class BindableBlobPath
    {
        public static IBindableBlobPath Create(string pattern, bool isContainerBinding = false)
        {
            BlobPath parsedPattern = BlobPath.Parse(pattern, isContainerBinding);
            BindingTemplate containerNameTemplate = BindingTemplate.FromString(parsedPattern.ContainerName);
            BindingTemplate blobNameTemplate = BindingTemplate.FromString(parsedPattern.BlobName);

            if (containerNameTemplate.HasParameters || blobNameTemplate.HasParameters)
            {
                return new ParameterizedBlobPath(containerNameTemplate, blobNameTemplate);
            }

            BlobClient.ValidateContainerName(parsedPattern.ContainerName);
            if (!string.IsNullOrEmpty(parsedPattern.BlobName))
            {
                BlobClient.ValidateBlobName(parsedPattern.BlobName);
            }
            return new BoundBlobPath(parsedPattern);
        }
    }
}
