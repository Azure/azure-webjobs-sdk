// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    public class EventHubExtensionConfigProvider : IExtensionConfigProvider
    {
        private readonly EventHubConfiguration _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConverterManager _converterManager;
        private readonly INameResolver _nameResolver;
        private readonly IConfiguration _configuration;

        public EventHubExtensionConfigProvider(EventHubConfiguration options, ILoggerFactory loggerFactory,
            IConverterManager converterManager, INameResolver nameResolver, IConfiguration configuration)
        {
            _options = options;
            _loggerFactory = loggerFactory;
            _converterManager = converterManager;
            _nameResolver = nameResolver;
            _configuration = configuration;
        }

        internal Action<ExceptionReceivedEventArgs> ExceptionHandler { get; set; }

        private void ExceptionReceivedHandler(ExceptionReceivedEventArgs args)
        {
            ExceptionHandler?.Invoke(args);
        }

        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // TODO: Can we bind these during service setup?
            // apply at eventProcessorOptions level (maxBatchSize, prefetchCount)

            EventProcessorOptions options = _options.GetOptions();
            options.SetExceptionHandler(ExceptionReceivedHandler);
            _configuration.Bind("eventHub", options);

            // apply at config level (batchCheckpointFrequency)
            _configuration.Bind("eventHub", _options);

            context
                .AddConverter<string, EventData>(ConvertString2EventData)
                .AddConverter<EventData, string>(ConvertEventData2String)
                .AddConverter<byte[], EventData>(ConvertBytes2EventData)
                .AddConverter<EventData, byte[]>(ConvertEventData2Bytes)
                .AddOpenConverter<OpenType.Poco, EventData>(ConvertPocoToEventData);

            // register our trigger binding provider
            var triggerBindingProvider = new EventHubTriggerAttributeBindingProvider(_nameResolver, _converterManager, _options, _loggerFactory);
            context.AddBindingRule<EventHubTriggerAttribute>()
                .BindToTrigger(triggerBindingProvider);

            // register our binding provider
            context.AddBindingRule<EventHubAttribute>()
                .BindToCollector(BuildFromAttribute);

            ExceptionHandler = (e =>
            {
                LogExceptionReceivedEvent(e, _loggerFactory);
            });
        }

        internal static void LogExceptionReceivedEvent(ExceptionReceivedEventArgs e, ILoggerFactory loggerFactory)
        {
            try
            {
                var logger = loggerFactory?.CreateLogger(LogCategories.Executor);
                string message = $"EventProcessorHost error (Action={e.Action}, HostName={e.Hostname}, PartitionId={e.PartitionId})";

                var ehex = e.Exception as EventHubsException;
                if (!(e.Exception is OperationCanceledException) && (ehex == null || !ehex.IsTransient))
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

        private IAsyncCollector<EventData> BuildFromAttribute(EventHubAttribute attribute)
        {
            EventHubClient client = _options.GetEventHubClient(attribute.EventHubName, attribute.Connection);
            return new EventHubAsyncCollector(client);
        }

        private static string ConvertEventData2String(EventData x)
            => Encoding.UTF8.GetString(ConvertEventData2Bytes(x));

        private static EventData ConvertBytes2EventData(byte[] input)
            => new EventData(input);

        private static byte[] ConvertEventData2Bytes(EventData input)
            => input.Body.Array;

        private static EventData ConvertString2EventData(string input)
            => ConvertBytes2EventData(Encoding.UTF8.GetBytes(input));

        private static Task<object> ConvertPocoToEventData(object arg, Attribute attrResolved, ValueBindingContext context)
        {
            return Task.FromResult<object>(ConvertString2EventData(JsonConvert.SerializeObject(arg)));
        }
    }
}
