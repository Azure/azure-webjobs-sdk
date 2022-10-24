// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Represents data a parameter binding provides that can be used by a function to perform the bind operation to the target itself.
    /// </summary>
    public sealed class ParameterBindingData
    {
        /// <summary>Initializes a new instance of the <see cref="ParameterBindingData"/> class</summary>
        /// <param name="version">The version of the binding data content</param>
        /// <param name="source">Identifies the extension the binding data is coming from</param>
        /// <param name="content">BinaryData containing the binding data</param>
        /// <param name="contentType">Content type of the binding data payload</param>
        /// <exception cref="ArgumentNullException">Throws if version, source, content or contentType is null.</exception>
        public ParameterBindingData(string version, string source, BinaryData content, string contentType)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        }

        /// <summary>
        /// The version of the binding data content
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// The extension source of the binding data i.e CosmosDB, AzureStorageBlobs
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// The binding data content.
        /// </summary>
        public BinaryData Content { get; }

        /// <summary>
        /// The content type of the binding data content i.e. "application/json"
        /// </summary>
        public string ContentType { get; }
    }
}
