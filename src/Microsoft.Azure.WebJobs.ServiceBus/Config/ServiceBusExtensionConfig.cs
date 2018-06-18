// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.ServiceBus.Bindings;
using Microsoft.Azure.WebJobs.ServiceBus.Triggers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.ServiceBus.Config
{
    /// <summary>
    /// Extension configuration provider used to register ServiceBus triggers and binders
    /// </summary>
    public class ServiceBusExtensionConfig : IExtensionConfigProvider
    {
        private readonly INameResolver _nameResolver;
        private readonly IConnectionStringProvider _connectionStringProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ServiceBusOptions _serviceBusConfig;
        private readonly MessagingProvider _messagingProvider;

        /// <summary>
        /// Creates a new <see cref="ServiceBusExtensionConfig"/> instance.
        /// </summary>
        /// <param name="serviceBusConfig">The <see cref="ServiceBusOptions"></see> to use./></param>
        public ServiceBusExtensionConfig(IOptions<ServiceBusOptions> serviceBusConfig,
            MessagingProvider messagingProvider,
            INameResolver nameResolver,
            IConnectionStringProvider connectionStringProvider,
            ILoggerFactory loggerFactory)
        {
            _serviceBusConfig = serviceBusConfig.Value;
            _messagingProvider = messagingProvider;
            _nameResolver = nameResolver;
            _connectionStringProvider = connectionStringProvider;
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        }

        /// <summary>
        /// Gets the <see cref="ServiceBusOptions"/>
        /// </summary>
        public ServiceBusOptions Config
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
                LogExceptionReceivedEvent(e, _loggerFactory);
            };

            // register our trigger binding provider
            ServiceBusTriggerAttributeBindingProvider triggerBindingProvider = new ServiceBusTriggerAttributeBindingProvider(_nameResolver, _serviceBusConfig, _messagingProvider, _connectionStringProvider);
            context.AddBindingRule<ServiceBusTriggerAttribute>().BindToTrigger(triggerBindingProvider);

            // register our binding provider
            ServiceBusAttributeBindingProvider bindingProvider = new ServiceBusAttributeBindingProvider(_nameResolver, _serviceBusConfig, _connectionStringProvider);
            context.AddBindingRule<ServiceBusAttribute>().Bind(bindingProvider);
        }

        internal static void LogExceptionReceivedEvent(ExceptionReceivedEventArgs e, ILoggerFactory loggerFactory)
        {
            try
            {
                var ctxt = e.ExceptionReceivedContext;
                var logger = loggerFactory?.CreateLogger(LogCategories.Executor);
                string message = $"MessageReceiver error (Action={ctxt.Action}, ClientId={ctxt.ClientId}, EntityPath={ctxt.EntityPath}, Endpoint={ctxt.Endpoint})";

                var sbex = e.Exception as ServiceBusException;
                if (!(e.Exception is OperationCanceledException) && (sbex == null || !sbex.IsTransient))
                {
                    // any non-transient exceptions or unknown exception types
                    // we want to log as errors
                    logger?.LogError(0, e.Exception, message);
                }
                else
                {
                    // transient errors we log as verbose so we have a record
                    // of them, but we don't treat them as actual errors
                    logger?.LogDebug(0, e.Exception, message);
                }
            }
            catch
            {
                // best effort logging
            }
        }
    }
}
