// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Class providing context information for a template parameter resolution.
    /// </summary>
    public class ParameterResolverContext
    {
        /// <summary>
        /// Gets or sets the binding template.
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// Gets or sets the name of the parameter being resolved.
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// Gets or sets the index into the <see cref="Template"/> where
        /// the parameter was parsed from.
        /// </summary>
        public int ParameterIndex { get; set; }

        /// <summary>
        /// Gets or sets the resolved value. When a <see cref="ParameterResolver"/> resolves a
        /// value it should set it here.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the binding data collection.
        /// </summary>
        public IReadOnlyDictionary<string, string> BindingData { get; set; }

        /// <summary>
        /// Gets or sets property values for the current bind operation.
        /// </summary>
        public IDictionary<string, object> Properties { get; set; }
    }
}
