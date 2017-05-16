// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// Built in object for all binding expressions. 
    /// </summary>
    internal class SysBindingData
    {
        // The name for this binding in the binding expressions. 
        public const string Name = "sys";

        // A name that can't be overwritten. 
        private const string InternalName = "$sys";

        public static readonly IReadOnlyDictionary<string, Type> DefaultSysContract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { Name, typeof(SysBindingData) }
        };

        /// <summary>
        /// The method name that the binding lives in. 
        /// The method name can be override by the <see cref="FunctionNameAttribute"/> 
        /// </summary>
        public string MethodName { get; set; }

        // Given a bindingData, extract just the sys binding data from it.
        // This can be used when resolving default contracts that shouldn't be using an instance binding data. 
        public static IReadOnlyDictionary<string, object> GetSysBindingData(IReadOnlyDictionary<string, object> bindingData)
        {
            var sys = GetFromData(bindingData);
            var sysBindingData = new Dictionary<string, object>
            {
                { Name, sys }
            };
            return sysBindingData;
        }

        public void AddToBindingData(Dictionary<string, object> bindingData)
        {
            // User data takes precedence, so if 'sys' already exists, add via the internal name. 
            string sysName = bindingData.ContainsKey(SysBindingData.Name) ? SysBindingData.InternalName : SysBindingData.Name;            
            bindingData[sysName] = this;
        }

        public static SysBindingData GetFromData(IReadOnlyDictionary<string, object> bindingData)
        {
            object val;
            if (bindingData.TryGetValue(InternalName, out val))
            {
                return val as SysBindingData;
            }
            if (bindingData.TryGetValue(Name, out val))
            {
                return val as SysBindingData;
            }
            return null;
        }
    }
}
