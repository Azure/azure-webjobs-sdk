// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.ServiceBus.Bindings;
using Microsoft.Azure.WebJobs.ServiceBus.Triggers;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus.Config
{
    /// <summary>
    /// Extension configuration provider used to register ServiceBus triggers and binders
    /// </summary>
    internal class ServiceBusExtensionConfig : IExtensionConfigProvider
    {
        private ServiceBusConfiguration _serviceBusConfig;

        /// <summary>
        /// Creates a new <see cref="ServiceBusExtensionConfig"/> instance.
        /// </summary>
        /// <param name="serviceBusConfig">The <see cref="ServiceBusConfiguration"></see> to use./></param>
        public ServiceBusExtensionConfig(ServiceBusConfiguration serviceBusConfig)
        {
            if (serviceBusConfig == null)
            {
                throw new ArgumentNullException("serviceBusConfig");
            }

            _serviceBusConfig = serviceBusConfig;
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

            // Register an exception handler for background exceptions
            // coming from MessageReceivers.
            //
            // The message options is a host level instance that is shared
            // across all bindings, so we have to subscribe to it at the
            // host level.
            Config.MessageOptions.ExceptionReceived += (s, e) =>
            {
                if (!e.Exception.IsWrappedExceptionTransient())
                {
                    string message = $"MessageReceiver error (Action={e.Action})";
                    var logger = context.Config.LoggerFactory?.CreateLogger(LogCategories.Executor);
                    logger?.LogError(0, e.Exception, message);

                    context.Trace.Error(message, e.Exception);
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