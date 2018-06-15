// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Azure.WebJobs.Hosting
{
    /// <summary>
    /// An implementation of an <see cref="IWebJobsStartupTypeDiscoverer"/> that locates startup types
    /// configured in the entry point assembly using the <see cref="WebJobsStartupAttribute"/>.
    /// </summary>
    internal class DefaultStartupTypeDiscoverer : IWebJobsStartupTypeDiscoverer
    {
        private readonly Assembly _entryAssembly;

        public DefaultStartupTypeDiscoverer()
        {
        }

        internal DefaultStartupTypeDiscoverer(Assembly entryAssembly)
        {
            _entryAssembly = entryAssembly;
        }

        public Type[] GetStartupTypes()
        {
            Assembly assembly = _entryAssembly ?? Assembly.GetEntryAssembly();
            IEnumerable<WebJobsStartupAttribute> startupAttributes = assembly.GetCustomAttributes<WebJobsStartupAttribute>();

            return startupAttributes.Select(a => a.WebJobsStartupType).ToArray();
        }
    }
}
