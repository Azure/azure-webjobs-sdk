// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host
{
    // Provides additional bookkeeping on extensions 
    internal class JobHostMetadataProvider : IJobHostMetadataProvider
    {
        // Map from binding types to their corresponding attribute. 
        private readonly IDictionary<string, Type> _attributeTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        // Map of simple assembly name to assembly.
        private readonly Dictionary<string, Assembly> _resolvedAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private readonly JobHostConfiguration _config;

        private IBindingProvider _root;

        public JobHostMetadataProvider(JobHostConfiguration config)
        {
            _config = config;
        }

        internal void Initialize(IBindingProvider bindingProvider)
        {
            this._root = bindingProvider;

            // Populate assembly resolution from converters.
            var converter = this._config.GetService<IConverterManager>() as ConverterManager;
            if (converter != null)
            {
                converter.AddAssemblies((type) => this.AddAssembly(type));
            }

            AddTypesFromGraph(bindingProvider as IBindingRuleProvider);
        }

        // Resolve an assembly from the given name. 
        // Name could be the short name or full name. 
        //    Name
        //    Name, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
        public bool TryResolveAssembly(string assemblyName, out Assembly assembly)
        {            
            // Give precedence to the full name. This can be important if multiple assemblies are loaded side-by-side.
            if (!_resolvedAssemblies.TryGetValue(assemblyName, out assembly))
            {
                // If full name fails, try on just the short name. 
                var nameOnly = new AssemblyName(assemblyName).Name;
                _resolvedAssemblies.TryGetValue(nameOnly, out assembly);
            }
            return assembly != null;
        }

        // USed by core extensions where the attribute lives in a different assembly than the extension. 
        internal void AddAttributesFromAssembly(Assembly asm)
        {
            var attributeTypes = GetAttributesFromAssembly(asm);
            foreach (var attributeType in attributeTypes)
            {
                string bindingName = GetNameFromAttribute(attributeType);
                this._attributeTypes[bindingName] = attributeType;
            }
        }

        private static IEnumerable<Type> GetAttributesFromAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                if (typeof(Attribute).IsAssignableFrom(type))
                {
                    if (type.GetCustomAttribute(typeof(BindingAttribute)) != null)
                    {
                        yield return type;
                    }                    
                }
            }
        }

        // Do extra bookkeeping for a new extension. 
        public void AddExtension(IExtensionConfigProvider extension)
        {
            AddAttributesFromAssembly(extension.GetType().Assembly);
            AddAssembly(extension.GetType());         
        }

        private void AddAssembly(Type type)
        {
            AddAssembly(type.Assembly);
        }

        private void AddAssembly(Assembly assembly)
        {
            AssemblyName name = assembly.GetName();
            _resolvedAssemblies[name.FullName] = assembly;
            _resolvedAssemblies[name.Name] = assembly;
        }

        // By convention, typeof(EventHubAttribute) --> "EventHub"
        private static string GetNameFromAttribute(Type attributeType)
        {
            string fullname = attributeType.Name; // no namespace
            const string Suffix = "Attribute";

            if (!fullname.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Attribute type '{fullname}' must end in 'Attribute'");
            }
            string name = fullname.Substring(0, fullname.Length - Suffix.Length);
            return name;
        }

        public Type GetAttributeTypeFromName(string name)
        {
            Type attrType;
            if (_attributeTypes.TryGetValue(name, out attrType))
            {
                return attrType;
            }
            return null;
        }

        public Attribute GetAttribute(Type attributeType, JObject metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException("metadata");
            }

            metadata = Touchups(attributeType, metadata);

            var resolve = AttributeCloner.CreateDirect(attributeType, metadata);
            return resolve;
        }

        // Handle touchups where automatically conversion would break. 
        // Ideally get rid of this method by either 
        // a) removing the inconsistencies
        // b) having some hook that lets the extension handle it. 
        private static JObject Touchups(Type attributeType, JObject metadata)
        {
            metadata = (JObject)metadata.DeepClone(); // avoid mutating the inpout 

            JToken token;
            if (attributeType == typeof(BlobAttribute) ||
                attributeType == typeof(BlobTriggerAttribute))
            {
                // Path --> BlobPath                
                if (metadata.TryGetValue("path", StringComparison.OrdinalIgnoreCase, out token))
                {
                    metadata["BlobPath"] = token;
                }

                if (metadata.TryGetValue("direction", StringComparison.OrdinalIgnoreCase, out token))
                {
                    FileAccess access;
                    switch (token.ToString().ToLowerInvariant())
                    {
                        case "in":
                            access = FileAccess.Read;
                            break;
                        case "out":
                            access = FileAccess.Write;
                            break;
                        case "inout":
                            access = FileAccess.ReadWrite;
                            break;
                        default:
                            throw new InvalidOperationException($"Illegal direction value: '{token}'");
                    }
                    metadata["access"] = access.ToString();
                }
            }

            return metadata;
        }
             
        // Get a better implementation 
        public Type GetDefaultType(
            Attribute attribute,
            FileAccess access, // direction In, Out, In/Out
            Type requestedType) // combination of Cardinality and DataType.
        {
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }
            if (requestedType == null)
            {
                requestedType = typeof(object);
            }
            var providers = this._root;

            IBindingRuleProvider root = (IBindingRuleProvider)providers;
            var type = root.GetDefaultType(attribute, access, requestedType);

            if ((type == null) && (access == FileAccess.Read))
            {
                // For input bindings, if we have a specific requested type, then return and try to bind against that. 
                // ITriggerBindingProvider doesn't provide rules. 
                if (requestedType != typeof(object))
                {
                    return requestedType;
                }
                else
                {
                    // common default. If binder doesn't support this, it will fail later in the pipeline. 
                    return typeof(String); 
                }
            }

            if (type == null)
            {
                throw new InvalidOperationException($"Can't bind {attribute.GetType().Name} to a script-compatible type for {access} access" + 
                    ((requestedType != null) ? $"to { requestedType.Name }." : "."));
            }
            return type;
        }

        /// <summary>
        /// Debug helper to dump the entire extension graph 
        /// </summary>        
        public void DebugDumpGraph(TextWriter output)
        {
            var providers = this._root;

            IBindingRuleProvider root = (IBindingRuleProvider)providers;
            DumpRule(root, output);
        }

        internal static void DumpRule(IBindingRuleProvider root, TextWriter output)
        {
            foreach (var rule in root.GetRules())
            {
                var attr = rule.SourceAttribute;

                output.Write($"[{attr.Name}] -->");
                if (rule.Filter != null)
                {
                    output.Write($"[filter: {rule.Filter}]-->");
                }

                if (rule.Converters != null)
                {
                    foreach (var converterType in rule.Converters)
                    {
                        output.Write($"{ConverterManager.ExactMatch.TypeToString(converterType)}-->");
                    }
                }

                output.Write(rule.UserType.GetDisplayName());
                output.WriteLine();
            }          
        }

        private static bool ApplyFilter(Attribute attribute, string filter)
        {
            if (filter == null)
            {
                return true;
            }

            var t = attribute.GetType();

            // $$$
            // ({0} == null)
            // ({0} != null)
            var parts = filter.Split(new string[] { " && " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                int idxSpace = part.IndexOf(' ');
                var name = part.Substring(1, idxSpace - 1);

                var prop = t.GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                var val = prop.GetValue(attribute);

                if (part.EndsWith(" == Read)", StringComparison.OrdinalIgnoreCase))
                {
                    if ((val == null) || ((FileAccess)val != FileAccess.Read))
                    {
                        return false;
                    }
                }
                if (part.EndsWith(" == Write)", StringComparison.OrdinalIgnoreCase))
                {
                    if ((val == null) || ((FileAccess)val != FileAccess.Write))
                    {
                        return false;
                    }
                }

                if (part.EndsWith(" == null)", StringComparison.OrdinalIgnoreCase))
                {
                    if (val != null)
                    {
                        return false;
                    }
                }
                if (part.EndsWith("!= null)", StringComparison.OrdinalIgnoreCase))
                {
                    if (val == null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public string[] CheckBindingErrors(Attribute attribute, Type targetType)
        {
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }
            var providers = this._root;

            HashSet<string> possible = new HashSet<string>();
            
            IBindingRuleProvider root = (IBindingRuleProvider)providers;
            foreach (var rule in root.GetRules())
            {
                var attr = rule.SourceAttribute;

                bool attrMatch = attr.FullName == attribute.GetType().FullName;
                bool typeMatch = rule.UserType.IsMatch(targetType);
                bool filterMatch = attrMatch && ApplyFilter(attribute, rule.Filter);
                
                if (attrMatch && filterMatch && typeMatch)
                {
                    // Success!
                    return null;
                }

                // Doesn't match. Add a possible hint about what would match. 
                if (typeMatch)
                {
                    if (attrMatch)
                    {
                        // Filter misatch 
                        possible.Add($"set {rule.Filter}");
                    }
                    else
                    {
                        // Possibly use another attribute to bind to this type?
                        possible.Add($"try [{attr.FullName}]");                        
                    }
                }
                else
                {
                    if (filterMatch && attrMatch)
                    {
                        possible.Add(rule.UserType.GetDisplayName());
                    }
                    else
                    {
                        // Rule is too unrelated 
                    }
                }
            }

            var x = possible.ToArray();
            Array.Sort(x);
            return x;
        }

        private void AddTypesFromGraph(IBindingRuleProvider root)
        {
            foreach (var rule in root.GetRules())
            {
                var type = rule.UserType as ConverterManager.ExactMatch;
                if (type != null)                
                {
                    AddAssembly(type.ExactType);
                }
            }
        }
    }
}