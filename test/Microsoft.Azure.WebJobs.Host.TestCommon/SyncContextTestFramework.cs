using System.Threading;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    /// <summary>
    /// Swaps Xunit's sync context with a new sync context to avoid Task deadlock,
    /// in particular for JobHost.Start / SingletonManager.HostId
    /// </summary>
    public class SyncContextTestFramework : XunitTestFramework
    {
        SynchronizationContext xunitContext;

        public SyncContextTestFramework(IMessageSink messageSink) : base(messageSink)
        {
            xunitContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        }

        ~SyncContextTestFramework()
        {
            SynchronizationContext.SetSynchronizationContext(xunitContext);
        }
    }
}
