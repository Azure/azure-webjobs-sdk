// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// A reference type for supporting SDK-type bindings in
    /// out-of-process Function workers
    /// </summary>
    public class ParameterBindingData
    {
        /// <summary>
        /// Parameter binding data version
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Indicates the original media type of the resource i.e. text/plain or application/json
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Extension source i.e CosmosDB, BlobStorage
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// An object containing any required information to hydrate
        /// an SDK-type object in the out-of-process worker
        /// </summary>
        public object Content { get; set; }
    }
}
