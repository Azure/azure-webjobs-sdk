// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Host.Hosting
{
    internal class WebJobsBuilderWithHostContext : WebJobsBuilder, ISupportsStartupInstantiation
    {
        private readonly HostBuilderContext _context;

        public WebJobsBuilderWithHostContext(
            IServiceCollection services,
            HostBuilderContext context) : base(services)
        {
            this._context = context;
        }

        public IWebJobsStartup CreateStartupInstance(Type startupType)
        {
            return (IWebJobsStartup)ActivatorUtilities.CreateInstance(new HostServiceProvider(_context), startupType);
        }

        private class HostServiceProvider : IServiceProvider
        {
            private readonly HostBuilderContext _context;

            public HostServiceProvider(HostBuilderContext context)
            {
                _context = context;
            }

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(Microsoft.Extensions.Hosting.IHostingEnvironment)
                    // Would need this if targetting Microsoft.Extensions.Hosting v3.x
                    //|| serviceType == typeof(IHostEnvironment)
                    )
                {
                    return _context.HostingEnvironment;
                }

                if (serviceType == typeof(IConfiguration))
                {
                    return _context.Configuration;
                }

                return null;
            }
        }
    }
}
