// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    /// <summary>
    /// Helper Extension methods for extension configuration. 
    /// </summary>
    [Obsolete("Not ready for public consumption.")]
    public static class ExtensionConfigContextExtensions
    {
        /// <summary>
        /// Get the configuration object for this extension. 
        /// </summary>
        /// <param name="context"></param>
        /// /// <param name="target"></param>
        /// <param name="section"></param>
        /// <returns></returns>
        public static JObject GetConfig(this ExtensionConfigContext context, object target, string section = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            JObject hostMetadata = context.Config.HostConfigMetadata;
            if (hostMetadata == null)
            {
                return null;
            }

            if (section == null)
            {
                // By convention, an extension named "FooConfiguration" reads the "Foo" section. 
                if (target is IExtensionConfigProvider)
                {
                    string name = target.GetType().Name;
                    var suffix = "Configuration";
                    if (name.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        section = name.Substring(0, name.Length - suffix.Length);
                    }
                }
            }

            if (section != null)
            {
                JToken value;
                if (hostMetadata.TryGetValue(section, StringComparison.OrdinalIgnoreCase, out value))
                {
                    hostMetadata = value as JObject;
                }
            }

            return hostMetadata;
        }

        /// <summary>
        /// Apply configuration for this extension. 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="target">an existing object that the settings will get JSON serialized onto</param>
        /// <param name="section">specifies a section from the overall config that should apply to this object.</param>
        public static void ApplyConfig(this ExtensionConfigContext context, object target, string section = null)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            JObject hostMetadata = context.GetConfig(target, section);
            if (hostMetadata == null)
            {
                return;
            }            

            JsonConvert.PopulateObject(hostMetadata.ToString(), target);
        }
    }
}
