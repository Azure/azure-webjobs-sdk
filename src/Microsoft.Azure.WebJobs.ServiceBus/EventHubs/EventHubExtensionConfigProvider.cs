using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    public class EventHubExtensionConfigProvider : IExtensionConfigProvider
    {
        private readonly EventHubConfiguration _config;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConverterManager _converterManager;
        private readonly INameResolver _nameResolver;
        private readonly IConfiguration _configuration;

        public EventHubExtensionConfigProvider(EventHubConfiguration config, ILoggerFactory loggerFactory,
            IConverterManager converterManager, INameResolver nameResolver, IConfiguration configuration)
        {
            _config = config;
            _loggerFactory = loggerFactory;
            _converterManager = converterManager;
            _nameResolver = nameResolver;
            _configuration = configuration;
        }

        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // TODO: Can we bind these during service setup?
            // apply at eventProcessorOptions level (maxBatchSize, prefetchCount)
            _configuration.Bind("eventHub", _config.GetOptions());

            // apply at config level (batchCheckpointFrequency)
            _configuration.Bind("eventHub", _config);

            context
                .AddConverter<string, EventData>(ConvertString2EventData)
                .AddConverter<EventData, string>(ConvertEventData2String)
                .AddConverter<byte[], EventData>(ConvertBytes2EventData)
                .AddConverter<EventData, byte[]>(ConvertEventData2Bytes)
                .AddOpenConverter<OpenType.Poco, EventData>(ConvertPocoToEventData);

            // register our trigger binding provider
            var triggerBindingProvider = new EventHubTriggerAttributeBindingProvider(_nameResolver, _converterManager, _config, _loggerFactory);
            context.AddBindingRule<EventHubTriggerAttribute>()
                .BindToTrigger(triggerBindingProvider);

            // register our binding provider
            context.AddBindingRule<EventHubAttribute>()
                .BindToCollector(BuildFromAttribute);
        }

        private IAsyncCollector<EventData> BuildFromAttribute(EventHubAttribute attribute)
        {
            EventHubClient client = _config.GetEventHubClient(attribute.EventHubName, attribute.Connection);
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
