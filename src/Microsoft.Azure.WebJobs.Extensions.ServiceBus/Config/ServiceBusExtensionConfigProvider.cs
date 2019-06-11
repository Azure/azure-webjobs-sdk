// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.ServiceBus.Bindings;
using Microsoft.Azure.WebJobs.ServiceBus.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.ServiceBus.Config
{
    /// <summary>
    /// Extension configuration provider used to register ServiceBus triggers and binders
    /// </summary>
    [Extension("ServiceBus")]
    internal class ServiceBusExtensionConfigProvider : IExtensionConfigProvider
    {
        private readonly INameResolver _nameResolver;
        private readonly IConfiguration _configuration;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ServiceBusOptions _options;
        private readonly MessagingProvider _messagingProvider;
        private readonly IConverterManager _converterManager;

        /// <summary>
        /// Creates a new <see cref="ServiceBusExtensionConfigProvider"/> instance.
        /// </summary>
        /// <param name="options">The <see cref="ServiceBusOptions"></see> to use./></param>
        public ServiceBusExtensionConfigProvider(IOptions<ServiceBusOptions> options,
            MessagingProvider messagingProvider,
            INameResolver nameResolver,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            IConverterManager converterManager)
        {
            _options = options.Value;
            _messagingProvider = messagingProvider;
            _nameResolver = nameResolver;
            _configuration = configuration;
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _converterManager = converterManager;
        }

        /// <summary>
        /// Gets the <see cref="ServiceBusOptions"/>
        /// </summary>
        public ServiceBusOptions Options
        {
            get
            {
                return _options;
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
            Options.ExceptionHandler = (e) =>
            {
                LogExceptionReceivedEvent(e, _loggerFactory);
            };

            context
                .AddConverter<string, Message>(ConvertString2Message)
                .AddConverter<Message, string>(ConvertMessage2String)
                .AddConverter<byte[], Message>(ConvertBytes2Message)
                .AddConverter<Message, byte[]>(ConvertMessage2Bytes)
                .AddOpenConverter<OpenType.Poco, Message>(ConvertPocoToMesage);

            // register our trigger binding provider
            ServiceBusTriggerAttributeBindingProvider triggerBindingProvider = new ServiceBusTriggerAttributeBindingProvider(_nameResolver, _options, _messagingProvider, _configuration, _converterManager);
            context.AddBindingRule<ServiceBusTriggerAttribute>().BindToTrigger(triggerBindingProvider);

            // register our output binding provider
            context.AddBindingRule<ServiceBusAttribute>()
                .BindToCollector(GetBusAsyncCollector);

            // register MessageSender output binding
            context.AddBindingRule<ServiceBusAttribute>().BindToValueProvider(GetMessageSender);
        }

        internal static void LogExceptionReceivedEvent(ExceptionReceivedEventArgs e, ILoggerFactory loggerFactory)
        {
            try
            {
                var ctxt = e.ExceptionReceivedContext;
                var logger = loggerFactory?.CreateLogger(LogCategories.Executor);
                string message = $"Message processing error (Action={ctxt.Action}, ClientId={ctxt.ClientId}, EntityPath={ctxt.EntityPath}, Endpoint={ctxt.Endpoint})";

                var logLevel = GetLogLevel(e.Exception);
                logger?.Log(logLevel, 0, message, e.Exception, (s, ex) => message);
            }
            catch
            {
                // best effort logging
            }
        }

        private ServiceBusAsyncCollector GetBusAsyncCollector(ServiceBusAttribute attribute)
        {
            ServiceBusAccount account = GetServiceBusAccount(attribute);
            MessageSender messageSender = _messagingProvider.CreateMessageSender(account.EntityPath, account.ConnectionString);
            return new ServiceBusAsyncCollector(messageSender);
        }

        private Task<IValueBinder> GetMessageSender(ServiceBusAttribute attribute, Type type)
        {
            ServiceBusAccount account = GetServiceBusAccount(attribute);
            MessageSender messageSender = _messagingProvider.CreateMessageSender(account.EntityPath, account.ConnectionString);
            MessageSenderBinder binder = new MessageSenderBinder(messageSender, messageSender.GetType());
            return Task.FromResult(binder as IValueBinder);
        }

        private ServiceBusAccount GetServiceBusAccount(ServiceBusAttribute attribute)
        {
            string queueOrTopicName = Resolve(attribute.QueueOrTopicName);
            attribute.Connection = Resolve(attribute.Connection);
            ServiceBusAccount account = new ServiceBusAccount(_options, _configuration, queueOrTopicName, attribute);

            return account;
        }

        private string Resolve(string queueName)
        {
            if (_nameResolver == null)
            {
                return queueName;
            }

            return _nameResolver.ResolveWholeString(queueName);
        }

        private static LogLevel GetLogLevel(Exception ex)
        {
            var sbex = ex as ServiceBusException;
            if (!(ex is OperationCanceledException) && (sbex == null || !sbex.IsTransient))
            {
                // any non-transient exceptions or unknown exception types
                // we want to log as errors
                return LogLevel.Error;
            }
            else
            {
                // transient messaging errors we log as info so we have a record
                // of them, but we don't treat them as actual errors
                return LogLevel.Information;
            }
        }

        private static string ConvertMessage2String(Message x)
            => Encoding.UTF8.GetString(ConvertMessage2Bytes(x));

        private static Message ConvertBytes2Message(byte[] input)
            => new Message(input);

        private static byte[] ConvertMessage2Bytes(Message input)
            => input.Body;

        private static Message ConvertString2Message(string input)
            => ConvertBytes2Message(Encoding.UTF8.GetBytes(input));

        private static Task<object> ConvertPocoToMesage(object arg, Attribute attrResolved, ValueBindingContext context)
        {
            return Task.FromResult<object>(ConvertString2Message(JsonConvert.SerializeObject(arg)));
        }

        private sealed class MessageSenderBinder : IValueBinder
        {
            private readonly object _messageSender;
            private readonly Type _type;

            public MessageSenderBinder(object messageSender, Type type)
            {
                _messageSender = messageSender;
                _type = type;
            }

            public Type Type => _type;

            public Task<object> GetValueAsync() => Task.FromResult(_messageSender);

            public string ToInvokeString() => null;

            public Task SetValueAsync(object value, CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
