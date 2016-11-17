// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Bindings // $$$
{
    /// <summary>
    /// </summary>
    // $$$ Instantiate at startup.  Search assembly for types; any that impl this interface get created, and we call Init on them.     
    public enum DataType
    {
        /// <summary>
        /// </summary>
        String,

        /// <summary>
        /// </summary>
        Binary,

        /// <summary>
        /// </summary>
        Stream
    }

    /// <summary>
    /// </summary>
    public enum Cardinality
    {
        /// <summary>
        /// Bind to a single element.
        /// </summary>
        One,

        /// <summary>
        /// Bind to many elements.
        /// </summary>
        Many
    }

    // Binding providers can implement this to be self-describing. 
    // this exposes them to automatic cross-language marshallers and better error handling. 
    internal interface IBindingProviderX
    {
        // Return non-null if found. 
        Type GetDefaultType(FileAccess access, Cardinality cardinality, DataType dataType, Attribute attr);
    }

    /// <summary>
    /// </summary>
    public class ToolingHelper
    {
        // Mapping from Attribute type to extension. 
        private readonly IDictionary<Type, ExtensionBase> _extensions = new Dictionary<Type, ExtensionBase>();

        // Map from binding types to their corresponding attribute. 
        private readonly IDictionary<string, Type> _attributeTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        // Map of assembly name to assembly.
        private readonly Dictionary<string, Assembly> _resolvedAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private JobHostConfiguration _config;

        /// <summary>
        /// </summary>
        /// <param name="config"></param>
        public ToolingHelper(JobHostConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            _config = config;           
        }

        /// <summary>        
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Type GetAttributeTypeFromName(string name)
        {
            Type attrType;
            if (_attributeTypes.TryGetValue(name, out attrType))
            {
                return attrType;
            }
            //throw new InvalidOperationException("Unknown binding type: " + name);
            return null;
        }

        private ExtensionBase GetExtensionForAttribute(Type attributeType)
        {
            ExtensionBase extension;
            _extensions.TryGetValue(attributeType, out extension);
            return extension;
        }

        /// <summary>
        /// Create an attribute from the metadata from Functions.Json 
        /// </summary>
        /// <param name="attributeType"></param>
        /// <param name="metadata"></param>
        /// <returns></returns>
        public Attribute[] GetAttributes(Type attributeType, JObject metadata)
        {            
            var extension = GetExtensionForAttribute(attributeType);
            return extension.GetAttributes(attributeType, metadata);
        }

        // Script lists direction as "In/out/Inout". Convert to a FileAccess enum for use 
        // with AttributeMetadata. 
        private static void TouchupDirectionProperty(JObject metadata)
        {
            JToken dir;
            if (metadata.TryGetValue("direction", StringComparison.OrdinalIgnoreCase, out dir))
            {
                FileAccess access;
                switch (dir.ToString().ToLowerInvariant())
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
                        return;
                }
                metadata["direction"] = access.ToString();
            }
        }

        // This is what the extension object calls to do the real work. 
        internal Attribute[] GetAttributesInternal(Type attributeType, JObject metadata)
        {
            var method  = this.GetType().GetMethod("GetAttributeGenericHelper", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var method2 = method.MakeGenericMethod(attributeType);
            var result = (Attribute[])method2.Invoke(this, new object[] { metadata });
            return result;
        }

        internal Attribute[] GetAttributeGenericHelper<TAttribute>(JObject metadata)
            where TAttribute : Attribute
        {
            var attr = BuildAttributeExplicit<TAttribute>(metadata);
            if (attr == null)
            {
                // Else, use attribute cloner to do it all automatically. 
                var resolver = this._config.NameResolver;

                attr = AttributeCloner<TAttribute>.CreateDirect(metadata, resolver);
            }
            return new Attribute[] { attr };
        }

        // check if TAttribute has an explicit override for building. 
        // Else return null and let AttribuetCloner handle it automatically.
        private TAttribute BuildAttributeExplicit<TAttribute>(JObject metadata)
            where TAttribute : Attribute
        {
            // If there's a nested type that implements AttributeMetadata, use that
            var attributeType = typeof(TAttribute);
            foreach (var nestedType in attributeType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (typeof(AttributeMetadata).IsAssignableFrom(nestedType))
                {
                    TouchupDirectionProperty(metadata);

                    var obj = (AttributeMetadata)metadata.ToObject(nestedType);

                    var attr = (TAttribute)obj.GetAttribute();

                    // Apply name resolver to solve %% 
                    var attrCloner = new AttributeCloner<TAttribute>(attr, null, this._config.NameResolver);
                    attr = attrCloner.GetNameResolvedAttribute();

                    return attr;
                }
            }
            return null;
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public async Task FinishAddsAsync()
        {
            await this.AddExtension(typeof(Indexers.DefaultExtensions), null); // $$$ need config, 

            var contextFactory = _config.GetJobHostContextFactory();

            var host = new JobHost(_config);

            var context = await contextFactory.CreateAndLogHostStartedAsync(
                host, CancellationToken.None, CancellationToken.None);
        }

        /// <summary>
        /// </summary>
        /// <param name="access"></param>
        /// <param name="cardinality"></param>
        /// <param name="dataType"></param>
        /// <param name="attribute"></param>
        /// <returns></returns>
        // Look down rules. 
        public Type GetDefaultType(FileAccess access, Cardinality cardinality, DataType dataType, Attribute attribute)
        {
            if (attribute == null)
            {
                throw new ArgumentNullException("attribute");
            }
            var extension = GetExtensionForAttribute(attribute.GetType());
            var type = extension.GetDefaultType(access, cardinality, dataType, attribute);
            return type;
        }
        
        // Hook from ExtensionBase. 
        internal Type GetDefaultTypeInternal(FileAccess access, Cardinality cardinality, DataType dataType, Attribute attribute)
        {
            Type type;

            var bindingProvider = _config.GetService<ITriggerBindingProvider>() as IBindingProviderX;
            if (bindingProvider != null)
            {
                type = bindingProvider.GetDefaultType(access, cardinality, dataType, attribute);
                if (type != null)
                {
                    return type;
                }
            }

            // Get list of binders from the config 
            bindingProvider = _config.GetService<IBindingProviderX>();

            type = bindingProvider.GetDefaultType(access, cardinality, dataType, attribute);
            return type;
        }

        /// <summary>
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="hostMetadata"></param>
        public async Task AddAssemblyAsync(Assembly assembly, JObject hostMetadata)
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                if (typeof(ExtensionBase).IsAssignableFrom(type))
                {                
                    await AddExtension(type, hostMetadata);
                }
            }
        }

        private ExtensionBase CreateExtension(Type extensionType)
        {
            Debug.Assert(this != null); // suppress StyleCop $$$

            var obj = Activator.CreateInstance(extensionType);
            
            var extension = (ExtensionBase)obj;
            extension.Tooling = this;
            return extension;
        }

        /// <summary>
        /// </summary>
        /// <param name="extensionType"></param>
        /// <param name="hostMetadata"></param>
        public async Task AddExtension(Type extensionType, JObject hostMetadata)
        {
            var extension = CreateExtension(extensionType);

            var attributeTypes = extension.ExposedAttributes;
            foreach (var attributeType in attributeTypes)
            {
                string bindingName = GetNameFromAttribute(attributeType);                
                this._attributeTypes[bindingName] = attributeType;
                this._extensions[attributeType] = extension;
            }

            if (extension.ResolvedAssemblies != null)
            {
                foreach (var resolvedAssembly in extension.ResolvedAssemblies)
                {
                    string name = resolvedAssembly.GetName().Name;
                    _resolvedAssemblies[name] = resolvedAssembly;
                }
            }

            await extension.InitAsync(_config, hostMetadata);
        }

        // By convention, typeof(EventHubAttribute) --> "EventHub"
        private static string GetNameFromAttribute(Type attributeType)
        {
            string fullname = attributeType.Name; // no namespace
            const string Suffix = "Attribute";

            if (!fullname.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Attribute type '" + fullname + "' must end in 'Attribute'");
            }
            string name = fullname.Substring(0, fullname.Length - Suffix.Length);
            return name;
        }

        /// <summary>
        /// Attempt to resolve the specified reference assembly.
        /// </summary>
        /// <remarks>
        /// This allows an extension to support "built in" assemblies for .NET functions so
        /// user code can easily reference them.
        /// </remarks>
        /// <param name="assemblyName">The name of the assembly to resolve.</param>
        /// <param name="assembly">The assembly if we were able to resolve.</param>
        /// <returns>True if the assembly could be resolved, false otherwise.</returns>
        public bool TryResolveAssembly(string assemblyName, out Assembly assembly)
        {
            return _resolvedAssemblies.TryGetValue(assemblyName, out assembly);            
        }
    }
}
