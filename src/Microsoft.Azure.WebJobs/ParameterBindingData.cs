// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// A reference type for supporting SDK-type bindings in out-of-process Function workers
    /// </summary>
    public class ParameterBindingData
    {
        /// <summary> Initializes a new instance of the <see cref="ParameterBindingData"/> class with the Content-Type set to application/json</summary>
        /// <param name="source"> Identifies the extension this event is coming from </param>
        /// <param name="jsonSerializableData"> An object containing any required information to hydrate an SDK-type object in the out-of-process worker </param>
        /// <param name="dataSerializationType"> The type to use when serializing the data.
        /// If not specified, <see cref="object.GetType()"/> will be used on <paramref name="jsonSerializableData"/>.</param>
        /// <exception cref="ArgumentNullException">Throws when source is null.</exception>
        /// <exception cref="ArgumentException">Throws when jsonSerializableData is of type BinaryData.</exception>
        public ParameterBindingData(string source, object jsonSerializableData, Type dataSerializationType = default)
        {
            if (jsonSerializableData is BinaryData)
            {
                throw new ArgumentException("This constructor does not support BinaryData. Use the constructor that takes a BinaryData instance.");
            }

            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Version = "1.0";
            Source = source;
            ContentType = "application/json";
            Content = new BinaryData(jsonSerializableData, type: dataSerializationType ?? jsonSerializableData?.GetType());
        }

        /// <summary> Initializes a new instance of the <see cref="ParameterBindingData"/> class using binary event data.</summary>
        /// <param name="source"> Identifies the extension this event is coming from </param>
        /// <param name="content"> Binary data containing any required information to hydrate an SDK-type object in the out-of-process worker </param>
        /// <param name="contentType"> Content type of the payload. A content type different from "application/json" should be specified if payload is not JSON. </param>
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
        /// Gets the schema version of the ParameterBindingData
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets the extension source of the event i.e CosmosDB, BlobStorage
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets the content type of the content data
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// Gets the event content as <see cref="BinaryData"/>. Using BinaryData, one can deserialize
        /// the payload into rich data, or access the raw JSON data using <see cref="BinaryData.ToString()"/>.
        /// </summary>
        public BinaryData Content { get; }
    }
}
