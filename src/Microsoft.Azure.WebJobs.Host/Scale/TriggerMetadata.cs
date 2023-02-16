// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Represents trigger metadata.
    /// </summary>
    public class TriggerMetadata
    {
        public TriggerMetadata(string functionName, JObject metadata)
            : this(functionName, metadata, new Dictionary<string, object>())
        {
        }

        public TriggerMetadata(string functionName, JObject metadata, IDictionary<string, object> properties)
        {
            Properties = properties;
            FunctionName = functionName;
            Metadata = metadata;
        }

        /// <summary>
        /// Gets the name of the Function this trigger belongs to.
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// Gets all the properties tagged to this instance.
        /// </summary>
        public IDictionary<string, object> Properties { get; }

        /// <summary>
        /// Gets the trigger metadata.
        /// </summary>
        public JObject Metadata { get; }
    }
}
