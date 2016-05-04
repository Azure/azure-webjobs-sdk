﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    // Clone an attribute and resolve it. 
    // This can be tricky since some read-only properties are set via the constructor. 
    // This assumes that the property name matches the constructor argument name. 
    internal class AttributeCloner<TAttribute>
        where TAttribute : Attribute
    {
        private readonly TAttribute _source;
        private readonly ConstructorInfo _bestCtor;
        private readonly Func<IReadOnlyDictionary<string, object>, object>[] _bestCtorArgBuilder;
        private readonly INameResolver _nameResolver;

        // Optional hook for post-processing the attribute. This is intended for legacy hack rules. 
        private readonly Func<TAttribute, Task<TAttribute>> _hook;

        public AttributeCloner(
            TAttribute source, 
            INameResolver nameResolver = null,
            Func<TAttribute, Task<TAttribute>> hook = null)
        {
            _hook = hook;
            _nameResolver = nameResolver ?? new EmptyNameResolver();
            _source = source;

            Type t = typeof(TAttribute);

            Dictionary<string, PropertyInfo> availableParams = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var objValue = prop.GetValue(_source);
                if (objValue != null)
                {
                    availableParams[prop.Name] = prop;
                }
            }

            int longestMatch = -1;

            // Pick the ctor with the longest parameter list where all parameters are matched. 
            var ctors = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in ctors)
            {
                var ps = ctor.GetParameters();
                int len = ps.Length;

                var getArgFuncs = new Func<IReadOnlyDictionary<string, object>, object>[len];

                bool hasAllParameters = true;
                for (int i = 0; i < len; i++)
                {
                    var p = ps[i];
                    PropertyInfo propInfo = null;
                    if (!availableParams.TryGetValue(p.Name, out propInfo))
                    {
                        hasAllParameters = false;
                        break;
                    }
                    bool resolve = propInfo.GetCustomAttribute<AutoResolveAttribute>() != null;
                    getArgFuncs[i] = (bindingData) =>
                    {
                        var propValue = propInfo.GetValue(_source);
                        if (resolve)
                        {
                            string str = (string)propValue;
                            if (str != null)
                            {
                                str = _nameResolver.ResolveWholeString(str);

                                BindingTemplate template = BindingTemplate.FromString(str);
                                propValue = template.Bind(bindingData);
                            }
                        }
                        return propValue;
                    };
                }

                if (hasAllParameters)
                {
                    if (len > longestMatch)
                    {
                        _bestCtor = ctor;
                        longestMatch = len;
                        _bestCtorArgBuilder = getArgFuncs;
                    }
                }
            }

            if (_bestCtor == null)
            {
                // error!!!
                throw new InvalidOperationException("Can't figure out which ctor to call.");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public string GetInvokeString(TAttribute attributeResolved)
        {
            var resolver = GetInvokeDescriptor(attributeResolved);
            string invokeString = resolver.ToInvokeString();
            return invokeString;
        }

        public async Task<TAttribute> ResolveFromInvokeString(string invokeString)
        {
            IAttributeInvokeDescriptor<TAttribute> resolver = GetInvokeDescriptor(_source);
            var attr = resolver.FromInvokeString(invokeString);
            if (_hook != null)
            {
                attr = await _hook(attr);
            }
            return attr;
        }
        
        private static IAttributeInvokeDescriptor<TAttribute> GetInvokeDescriptor(TAttribute attr)
        {
            var resolver = attr as IAttributeInvokeDescriptor<TAttribute>;
            if (resolver == null)
            {
                resolver = new DefaultAttributeInvokerDescriptor<TAttribute>(attr);
            }
            return resolver;
        }

        public async Task<TAttribute> ResolveFromBindingData(BindingContext ctx)
        {
            var attr = ResolveFromBindings(ctx.BindingData);
            if (_hook != null)
            {
                attr = await _hook(attr);
            }
            return attr;
        }

        // When there's only 1 resolvable property
        internal TAttribute New(string invokeString)
        {
            IDictionary<string, string> overrideProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Type t = typeof(TAttribute);
            foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                bool resolve = prop.GetCustomAttribute<AutoResolveAttribute>() != null;
                if (resolve)
                {
                    overrideProperties[prop.Name] = invokeString;
                }
            }
            if (overrideProperties.Count != 1)
            {
                throw new InvalidOperationException("Invalid invoke string format for attribute.");
            }
            return New(overrideProperties);
        }

        // Clone the source attribute, but override the properties with the supplied. 
        internal TAttribute New(IDictionary<string, string> overrideProperties)
        {
            IDictionary<string, object> propertyValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Populate inititial properties from the source
            Type t = typeof(TAttribute);
            foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                propertyValues[prop.Name] = prop.GetValue(_source);
            }

            foreach (var kv in overrideProperties)
            {
                propertyValues[kv.Key] = kv.Value;
            }

            var ctorArgs = Array.ConvertAll(_bestCtor.GetParameters(), param => propertyValues[param.Name]);
            var newAttr = (TAttribute)_bestCtor.Invoke(ctorArgs);

            foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (prop.CanWrite)
                {
                    var val = propertyValues[prop.Name];
                    prop.SetValue(newAttr, val);
                }
            }
            return newAttr;
        }

        internal TAttribute ResolveFromBindings(IReadOnlyDictionary<string, object> bindingData)
        {
            // Invoke ctor
            var ctorArgs = Array.ConvertAll(_bestCtorArgBuilder, func => func(bindingData));
            var newAttr = (TAttribute)_bestCtor.Invoke(ctorArgs);

            // Modify any settable properties 
            Type t = typeof(TAttribute);
            foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!prop.CanWrite)
                {
                    continue;
                }
                bool resolve = prop.GetCustomAttribute<AutoResolveAttribute>() != null;

                var objValue = prop.GetValue(_source);
                if (resolve)
                {
                    string str = (string)objValue;
                    if (str != null)
                    {
                        str = _nameResolver.ResolveWholeString(str);
                        BindingTemplate template = BindingTemplate.FromString(str);
                        objValue = template.Bind(bindingData);
                    }
                }
                prop.SetValue(newAttr, objValue);
            }
            return newAttr;
        }

        // If no name resolver is specified, then any %% becomes an error. 
        private class EmptyNameResolver : INameResolver
        {
            public string Resolve(string name)
            {
                return null;
            }
        }
    }
}