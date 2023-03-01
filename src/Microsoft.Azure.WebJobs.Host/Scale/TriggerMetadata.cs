// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Represents trigger metadata.
    /// </summary>
    public class TriggerMetadata
    {
        public TriggerMetadata(JObject metadata)
            : this(metadata, new Dictionary<string, object>())
        {
        }

        public TriggerMetadata(JObject metadata, IDictionary<string, object> properties)
        {
            Metadata = metadata;
            Properties = properties;
        }

        /// <summary>
        /// Gets the name of the Function this trigger belongs to.
        /// </summary>
        public string FunctionName => Metadata.GetValue("functionName", StringComparison.OrdinalIgnoreCase)?.Value<string>();

        /// <summary>
        /// Gets the type of the trigger.
        /// </summary>
        public string Type => Metadata.GetValue("type", StringComparison.OrdinalIgnoreCase)?.Value<string>();

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
