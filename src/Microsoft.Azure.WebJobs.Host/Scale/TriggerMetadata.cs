// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Represents trigger metadata.
    /// </summary>
    public class TriggerMetadata
    {
        private readonly string _functionName;
        private readonly string _type;

        public TriggerMetadata(JObject metadata)
            : this(metadata, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase))
        {
        }

        public TriggerMetadata(JObject metadata, IDictionary<string, object> properties)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Properties = properties ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            _functionName = Metadata.GetValue("functionName", StringComparison.OrdinalIgnoreCase)?.Value<string>();
            _type = Metadata.GetValue("type", StringComparison.OrdinalIgnoreCase)?.Value<string>();
        }

        /// <summary>
        /// Gets the name of the Function this trigger belongs to.
        /// </summary>
        public string FunctionName => _functionName;

        /// <summary>
        /// Gets the type of the trigger.
        /// </summary>
        public string Type => _type;

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
