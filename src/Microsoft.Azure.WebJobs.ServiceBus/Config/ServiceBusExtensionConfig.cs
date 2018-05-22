// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.ServiceBus.Bindings;
using Microsoft.Azure.WebJobs.ServiceBus.Triggers;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.ServiceBus.Config
{
    /// <summary>
    /// Extension configuration provider used to register ServiceBus triggers and binders
    /// </summary>
    public class ServiceBusExtensionConfig : IExtensionConfigProvider
    {
        private ServiceBusConfiguration _serviceBusConfig;

        /// <summary>
        /// default constructor. Callers can reference this without having any assembly references to service bus assemblies. 
        /// </summary>
        public ServiceBusExtensionConfig()
            : this(null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="ServiceBusExtensionConfig"/> instance.
        /// </summary>
        /// <param name="serviceBusConfig">The <see cref="ServiceBusConfiguration"></see> to use./></param>
        public ServiceBusExtensionConfig(ServiceBusConfiguration serviceBusConfig)
        {
            _serviceBusConfig = serviceBusConfig != null ? serviceBusConfig : new ServiceBusConfiguration();
        }

        /// <summary>
        /// Gets the <see cref="ServiceBusConfiguration"/>
        /// </summary>
        public ServiceBusConfiguration Config
        {
            get
            {
                return _serviceBusConfig;
            }
        }

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            // Set the default exception handler for background exceptions
            // coming from MessageReceivers.
            Config.ExceptionHandler = (e) =>
            {
                var sbex = e.Exception as ServiceBusException;
                if (sbex != null && !sbex.IsTransient)
                {
                    var ctxt = e.ExceptionReceivedContext;
                    string message = $"MessageReceiver error (Action={ctxt.Action}, ClientId={ctxt.ClientId}, EntityPath={ctxt.EntityPath}, Endpoint={ctxt.Endpoint})";
                    var logger = context.Config.LoggerFactory?.CreateLogger(LogCategories.Executor);
                    logger?.LogError(0, e.Exception, message);
                }
            };

            // get the services we need to construct our binding providers
            INameResolver nameResolver = context.Config.GetService<INameResolver>();
            IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();

            // register our trigger binding provider
            ServiceBusTriggerAttributeBindingProvider triggerBindingProvider = new ServiceBusTriggerAttributeBindingProvider(nameResolver, _serviceBusConfig);
            extensions.RegisterExtension<ITriggerBindingProvider>(triggerBindingProvider);

            // register our binding provider
            ServiceBusAttributeBindingProvider bindingProvider = new ServiceBusAttributeBindingProvider(nameResolver, _serviceBusConfig);
            extensions.RegisterExtension<IBindingProvider>(bindingProvider);
        }
    }
}
