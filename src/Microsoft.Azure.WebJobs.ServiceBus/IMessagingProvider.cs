using Microsoft.Azure.ServiceBus.Core;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    public interface IMessagingProvider
    {
        MessageProcessor CreateMessageProcessor(string entityPath, string connectionString);
        MessageReceiver CreateMessageReceiver(string entityPath, string connectionString);
    }
}
