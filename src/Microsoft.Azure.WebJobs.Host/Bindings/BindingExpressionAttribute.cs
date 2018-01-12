// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute to allow binding directly to binding data. 
    /// </summary>
    [Binding]
    public class BindingExpressionAttribute : Attribute
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="source">binding expression, such as '{x}' or '%appsetting%'. </param>
        public BindingExpressionAttribute(string source)
        {
            this.Source = source;
        }

        /// <summary>
        /// Binding expression for where to pull data. 
        /// </summary>
        [AutoResolve]
        public string Source { get; set; }
    }
}