// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// Builtin object for all binding expressions. 
    /// </summary>
    internal class SysBindingData
    {
        // The name for this binding in the binding expressions. 
        public const string Name = "sys";
                
        public static readonly IReadOnlyDictionary<string, Type> DefaultSysContract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { Name, typeof(SysBindingData) }
        };

        /// <summary>
        /// The method name that the binding lives in. 
        /// The method name can be override by the <see cref="FunctionNameAttribute"/> 
        /// </summary>
        public string MethodName { get; set; }
    }
}
