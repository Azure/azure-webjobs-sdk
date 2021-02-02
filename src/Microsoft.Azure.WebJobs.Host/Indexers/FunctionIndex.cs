// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.Indexers
{
    internal class FunctionIndex : IFunctionIndex, IFunctionIndexCollector
    {
        private readonly IDictionary<string, IFunctionDefinition> _functionsById;
        private readonly IDictionary<MethodInfo, IFunctionDefinition> _functionsByMethod;
        private readonly IDictionary<string, IFunctionDefinition> _functionsByName;
        private readonly ICollection<FunctionDescriptor> _functionDescriptors;

        public FunctionIndex()
        {
            _functionsById = new Dictionary<string, IFunctionDefinition>(StringComparer.OrdinalIgnoreCase);
            _functionsByMethod = new Dictionary<MethodInfo, IFunctionDefinition>();
            _functionsByName = new Dictionary<string, IFunctionDefinition>(StringComparer.OrdinalIgnoreCase);
            _functionDescriptors = new List<FunctionDescriptor>();
        }

        public void Add(IFunctionDefinition function, FunctionDescriptor descriptor, MethodInfo method)
        {
            if (_functionsById.ContainsKey(descriptor.Id))
            {
                throw new InvalidOperationException($"Method overloads are not supported. There are multiple methods with the name '{descriptor.Id}'.");
            }

            _functionsById.Add(descriptor.Id, function);
            _functionsByMethod.Add(method, function);

            // For compat, accept either the short name ("Class.Name") or log name (just "Name")
            if (!_functionsByName.ContainsKey(descriptor.LogName))
            {
                // since there can be duplicate method names across job classes, it's first one
                // wins for this cache
                _functionsByName.Add(descriptor.LogName, function);
            }
            if (descriptor.ShortName != descriptor.LogName &&
                !_functionsByName.ContainsKey(descriptor.ShortName))
            {
                // we do a duplicate check here as well for completeness, though a conflict here
                // is much less likely (could only happen if functions are coming from multiple assemblies).
                _functionsByName.Add(descriptor.ShortName, function);
            }

            _functionDescriptors.Add(descriptor);
        }

        public IFunctionDefinition Lookup(string functionId)
        {
            if (!_functionsById.TryGetValue(functionId, out IFunctionDefinition function))
            {
                return null;
            }
            return function;
        }

        public IFunctionDefinition LookupByName(string name)
        {
            if (!_functionsByName.TryGetValue(name, out IFunctionDefinition function))
            {
                return null;
            }
            return function;
        }

        public IFunctionDefinition Lookup(MethodInfo method)
        {
            if (!_functionsByMethod.TryGetValue(method, out IFunctionDefinition function))
            {
                return null;
            }
            return function;
        }

        public IEnumerable<IFunctionDefinition> ReadAll()
        {
            return _functionsById.Values;
        }

        public IEnumerable<FunctionDescriptor> ReadAllDescriptors()
        {
            return _functionDescriptors;
        }

        public IEnumerable<MethodInfo> ReadAllMethods()
        {
            return _functionsByMethod.Keys;
        }
    }
}