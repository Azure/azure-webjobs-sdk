// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// Built in object for all binding expressions. 
    /// </summary>
    /// <remarks>
    /// It's expected this class is created and added to the binding data. 
    /// </remarks>
    internal class SystemBindingData
    {
        // The public name for this binding in the binding expressions. 
        public const string Name = "sys";

        // An internal name for this binding that uses characters that gaurantee it can't be overwritten by a user. 
        // This ensures that we can always unambiguously retrieve this later. 
        private const string InternalKeyName = "$sys";

        private static readonly IReadOnlyDictionary<string, Type> DefaultSysContract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { Name, typeof(SystemBindingData) }
        };

        /// <summary>
        /// The method name that the binding lives in. 
        /// The method name can be override by the <see cref="FunctionNameAttribute"/> 
        /// </summary>
        public string MethodName { get; set; }

        // Given a bindingData, extract just the sys binding data from it.
        // This can be used when resolving default contracts that shouldn't be using an instance binding data. 
        internal static IReadOnlyDictionary<string, object> GetSysBindingData(IReadOnlyDictionary<string, object> bindingData)
        {
            var sys = GetFromData(bindingData);
            var sysBindingData = new Dictionary<string, object>
            {
                { Name, sys }
            };
            return sysBindingData;
        }

        // Validate that a template only uses static (non-instance) binding variables. 
        // Enforces we're not referring to other data from the trigger. 
        internal static void ValidateStaticContract(BindingTemplate template)
        {            
            try
            {
                template.ValidateContractCompatibility(SystemBindingData.DefaultSysContract);
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException($"Default contract can only refer to the '{SystemBindingData.Name}' binding data: " + e.Message);
            }
        }

        internal void AddToBindingData(Dictionary<string, object> bindingData)
        {
            // User data takes precedence, so if 'sys' already exists, add via the internal name. 
            string sysName = bindingData.ContainsKey(SystemBindingData.Name) ? SystemBindingData.InternalKeyName : SystemBindingData.Name;            
            bindingData[sysName] = this;
        }

        internal static SystemBindingData GetFromData(IReadOnlyDictionary<string, object> bindingData)
        {
            object val;
            if (bindingData.TryGetValue(InternalKeyName, out val))
            {
                return val as SystemBindingData;
            }
            if (bindingData.TryGetValue(Name, out val))
            {
                return val as SystemBindingData;
            }
            return null;
        }
    }
}
