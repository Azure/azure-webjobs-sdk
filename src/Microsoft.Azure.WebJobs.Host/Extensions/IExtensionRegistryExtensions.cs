// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Provides extension methods for <see cref="IExtensionRegistry"/>./>
    /// </summary>
    public static class IExtensionRegistryExtensions
    {
        /// <summary>
        /// Registers the specified instance. 
        /// </summary>
        /// <typeparam name="TExtension">The service type to register the instance for.</typeparam>
        /// <param name="registry">The registry instance.</param>
        /// <param name="extension">The instance to register.</param>
        public static void RegisterExtension<TExtension>(this IExtensionRegistry registry, TExtension extension)
        {
            registry.RegisterExtension(typeof(TExtension), extension);
        }
    }
}
