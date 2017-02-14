// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Bindings // $$$
{
    /// <summary>
    /// Each extension implements this. 
    /// </summary>
    public abstract class ExtensionBase
    {
        /// <summary>
        /// Get a mapping of names and attributes exposed by this extension. 
        /// For example, "EventHub" --> "EventHubAttribute"
        /// </summary>
        protected internal abstract IEnumerable<Type> ExposedAttributes { get; }
        
        /// <summary>
        /// Optionally expose a set of specific assemblies for resolution. 
        /// </summary>
        /// <remarks>
        /// This allows an extension to support "built in" assemblies for .NET functions so
        /// user code can easily reference them.
        /// </remarks>
        public Assembly[] ResolvedAssemblies { get; set; }

        // Backpointer to tooling for use with default implementations of GetAttributes() and GetDefaultType(). 
        // Internal since configuration authors should go through this base class rather than call 
        // the tooling APIs directly.
        internal ToolingHelper Tooling { get; set; }

        /// <summary>
        /// Initialize this extension and register it with the JobHostConfiguration. 
        /// </summary>
        /// <param name="config">job host configuration. </param>
        /// <param name="metadata">property bag to configure this extension.</param>
        /// <returns></returns>
        public abstract Task InitAsync(JobHostConfiguration config, JObject metadata);

        /// <summary>
        /// Instantiate an attribute given a property bag to use as constructor parameters and property values. 
        /// A derived class can use the base class in its implementation. 
        /// </summary>
        /// <param name="attributeType"></param>
        /// <param name="metadata"></param>
        /// <returns></returns>
        public virtual Attribute[] GetAttributes(Type attributeType, JObject metadata)
        {
            return this.Tooling.GetAttributesInternal(attributeType, metadata);
        }

        /// <summary>
        /// </summary>
        /// <param name="access"></param>
        /// <param name="cardinality"></param>
        /// <param name="dataType"></param>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public virtual Type GetDefaultType(FileAccess access, Cardinality cardinality, DataType dataType, Attribute attribute)
        {
            return this.Tooling.GetDefaultTypeInternal(access, cardinality, dataType, attribute);
        }
    }
}