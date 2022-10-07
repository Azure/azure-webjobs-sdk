// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// A reference type for supporting SDK-type bindings in out-of-process Function workers
    /// This type has built-in serialization using System.Text.Json.
    /// </summary>
    [JsonConverter(typeof(ParameterBindingData))]
    public class ParameterBindingData
    {
        /// <summary> Initializes a new instance of the <see cref="ParameterBindingData"/> class. </summary>
        /// <param name="source"> Identifies the extension this event is coming from </param>
        /// <param name="jsonSerializableData"> An object containing any required information to hydrate an SDK-type object in the out-of-process worker </param>
        /// <param name="dataSerializationType"> The type to use when serializing the data.
        /// If not specified, <see cref="object.GetType()"/> will be used on <paramref name="jsonSerializableData"/>.</param>
        public ParameterBindingData(string source, object jsonSerializableData, Type dataSerializationType = default)
        {
            if (jsonSerializableData is BinaryData)
            {
                throw new InvalidOperationException("This constructor does not support BinaryData. Use the constructor that takes a BinaryData instance.");
            }

            Version = "1.0";
            Source = source;
            ContentType = "application/json";
            Content = new BinaryData(jsonSerializableData, type: dataSerializationType ?? jsonSerializableData?.GetType());
        }

        /// <summary> Initializes a new instance of the <see cref="ParameterBindingData"/> class using binary event data.</summary>
        /// <param name="source"> Identifies the extension this event is coming from </param>
        /// <param name="content"> Binary data containing any required information to hydrate an SDK-type object in the out-of-process worker </param>
        /// <param name="dataContentType"> Content type of the payload. A content type different from "application/json" should be specified if payload is not JSON. </param>
        public ParameterBindingData(string source, BinaryData content, string dataContentType)
        {
            Version = "1.0";
            Source = source;
            ContentType = dataContentType;
            Content = content;
        }

        /// <summary>
        /// The schema version of the ParameterBindingData
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the extension source of the event i.e CosmosDB, BlobStorage
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets the content type of the content data
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the event content as <see cref="BinaryData"/>. Using BinaryData, one can deserialize
        /// the payload into rich data, or access the raw JSON data using <see cref="BinaryData.ToString()"/>.
        /// </summary>
        public BinaryData Content { get; set; }
    }
}
