// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.Azure.WebJobs.Script.Abstractions.Description.Binding
{
    public class ParameterBindingData
    {
        /// <summary>
        /// Gets or Sets all the properties of ReferenceType
        /// The storage account connection string
        /// The name of the container where the blob exists
        /// The name of the blob
        /// </summary>
        public IDictionary<string, string> Properties { get; set; }
    }
}
