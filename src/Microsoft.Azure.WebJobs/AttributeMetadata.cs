// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Base class for describing serialized for of an attribute. 
    /// This is only needed when there are name mismatches between the serialized form and the attribute property. 
    /// For example, "path" --> "blobPath". 
    /// If the names all match, then AttributeCloner can do the conversion for us automatically. 
    /// </summary>
    public abstract class AttributeMetadata
    {
        /// <summary>
        /// String name of attribute. 
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Direction of data flow. 
        /// </summary>
        public FileAccess Direction { get; set; }

        /// <summary>
        /// Parameter name this is on. 
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// get an instantiated attribute. 
        /// </summary>
        /// <returns></returns>
        public abstract Attribute GetAttribute();
    }
}