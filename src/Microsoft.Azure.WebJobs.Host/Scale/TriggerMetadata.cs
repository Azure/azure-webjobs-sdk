// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Represents trigger metadata.
    /// </summary>
    public class TriggerMetadata
    {
        private readonly List<object> _properties;

        public TriggerMetadata()
        {
            _properties = new List<object>();
        }

        /// <summary>
        /// Gets or sets triggers metadata <see cref="JObject"> value.
        /// </summary>
        public JObject Value { get; set; }

        /// <summary>
        /// Gets property by type.
        /// </summary>
        /// <typeparam name="T">Type of the property to get.</typeparam>
        public T GetProperty<T>()
        {
            return (T)_properties.SingleOrDefault(x => x is T);
        }

        internal void AddProperties(IEnumerable<object> properties)
        {
            _properties.AddRange(properties);
        }
    }
}
