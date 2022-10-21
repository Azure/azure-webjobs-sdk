// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Represents data a parameter binding provides that can be used by a function to perform the bind operation to the target itself.
    /// </summary>
    sealed public class ParameterBindingData
    {
        /// <summary>Initializes a new instance of the <see cref="ParameterBindingData"/> class using the specified BindingData and content type.</summary>
        /// <param name="source">Identifies the extension this event is coming from</param>
        /// <param name="content">BinaryData containing the binding data</param>
        /// <param name="contentType">Content type of the payload. A content type different from "application/json" should be specified if payload is not JSON.</param>
        /// <exception cref="ArgumentNullException">Throws if source, content or contentType is null.</exception>
        public ParameterBindingData(string source, BinaryData content, string contentType)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (contentType is null)
            {
                throw new ArgumentNullException(nameof(contentType));
            }

            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            Version = "1.0";
            Source = source;
            ContentType = contentType;
            Content = content;
        }

        /// <summary>
        /// The schema version of the ParameterBindingData
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// The extension source of the event i.e CosmosDB, BlobStorage
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// The content type of the content data
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// The binding data content.
        /// </summary>
        public BinaryData Content { get; }
    }
}
