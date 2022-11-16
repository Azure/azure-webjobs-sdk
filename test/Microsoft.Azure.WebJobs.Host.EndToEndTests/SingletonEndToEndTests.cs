// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public partial class SingletonEndToEndTests : IClassFixture<SingletonEndToEndTests.TestFixture>
    {
        private const string TestHostId = "e2etesthost";
        private const string TestArtifactsPrefix = "singletone2e";
        private const string Queue1Name = TestArtifactsPrefix + "q1%rnd%";
        private const string Queue2Name = TestArtifactsPrefix + "q2%rnd%";
        private const string Secondary = "SecondaryStorage";
        private Random _rand = new Random(314159);

        private static RandomNameResolver _resolver = new TestNameResolver();

        private static BlobContainerClient _lockDirectoryContainerClient;
        private static BlobContainerClient _secondaryLockDirectoryContainerClient;

        public SingletonEndToEndTests()
        {
            TestJobs.Reset();
            TestTriggerAttributeBindingProvider.TestTriggerBinding.TestTriggerListener.StartCount = 0;
        }

        [Fact]
        public async Task SingletonNonTriggeredFunction_MultipleConcurrentInvocations_InvocationsAreSerialized()
        {
            IHost host = CreateTestJobHost(1);
            host.Start();

            // make a bunch of parallel invocations
            int numInvocations = 20;
            List<Task> invokeTasks = new List<Task>();
            MethodInfo method = typeof(TestJobs).GetMethod(nameof(TestJobs.SingletonJob));
            for (int i = 0; i < numInvocations; i++)
            {
                int zone = _rand.Next(3) + 1;

                WorkItem workItem = new WorkItem
                {
                    ID = i + 1,
                    Region = "Central",
                    Zone = zone,
                    Category = 3,
                    Description = "Test Work Item " + i
                };
                invokeTasks.Add(host.GetJobHost().CallAsync(method, new { workItem = workItem }));
            }

            await Task.WhenAll(invokeTasks);

            Assert.False(TestJobs.FailureDetected);
            Assert.Equal(numInvocations, TestJobs.JobInvocations[1]);

            await VerifyLeaseState(method, SingletonScope.Function, "TestValue", LeaseState.Available, LeaseStatus.Unlocked);

            await host.StopAsync();
            host.Dispose();
        }

        [Fact(Skip = "Fails on ADO agent; investigate post-migration.")]
        public async Task SingletonListener_MultipleHosts_OnlyOneHostRunsListener()
        {
            // create and start multiple hosts concurrently
            int numHosts = 3;
            List<IHost> hosts = new List<IHost>();
            Task[] tasks = new Task[numHosts];
            for (int i = 0; i < numHosts; i++)
            {
                IHost host = CreateTestJobHost(i);
                hosts.Add(host);
                tasks[i] = host.StartAsync();
            }
            await Task.WhenAll(tasks);

            // verify that only 2 listeners were started (one for each of the singleton functions)
            await TestHelpers.Await(() =>
            {
                return TestTriggerAttributeBindingProvider.TestTriggerBinding.TestTriggerListener.StartCount == 3;
            });
            Assert.Equal(3, TestTriggerAttributeBindingProvider.TestTriggerBinding.TestTriggerListener.StartCount);

            MethodInfo singletonListenerMethod = typeof(TestJobs).GetMethod("TriggerJob_SingletonListener");
            await VerifyLeaseState(singletonListenerMethod, SingletonScope.Function, "Listener", LeaseState.Leased, LeaseStatus.Locked);

            MethodInfo singletonListenerAndFunctionMethod = typeof(TestJobs).GetMethod("SingletonTriggerJob_SingletonListener");
            await VerifyLeaseState(singletonListenerAndFunctionMethod, SingletonScope.Function, "Listener", LeaseState.Leased, LeaseStatus.Locked);

            // stop all the hosts
            foreach (IHost host in hosts)
            {
                await host.StopAsync();
                host.Dispose();
            }

            await VerifyLeaseState(singletonListenerMethod, SingletonScope.Function, "Listener", LeaseState.Available, LeaseStatus.Unlocked);
            await VerifyLeaseState(singletonListenerAndFunctionMethod, SingletonScope.Function, "Listener", LeaseState.Available, LeaseStatus.Unlocked);
        }

        [Fact(Skip = "Fails on ADO agent; investigate post-migration.")]
        public async Task SingletonListener_SingletonFunction_InvocationsAreSerialized()
        {
            IHost host = CreateTestJobHost(1);
            await host.StartAsync();

            MethodInfo singletonListenerAndFunctionMethod = typeof(TestJobs).GetMethod("SingletonTriggerJob_SingletonListener");
            await VerifyLeaseState(singletonListenerAndFunctionMethod, SingletonScope.Function, "Listener", LeaseState.Leased, LeaseStatus.Locked);

            await host.GetJobHost().CallAsync(singletonListenerAndFunctionMethod, new { test = "Test" });

            await host.StopAsync();
            host.Dispose();

            await VerifyLeaseState(singletonListenerAndFunctionMethod, SingletonScope.Function, "TestScopeTestValue", LeaseState.Available, LeaseStatus.Unlocked);
            await VerifyLeaseState(singletonListenerAndFunctionMethod, SingletonScope.Function, "Listener", LeaseState.Available, LeaseStatus.Unlocked);
        }

        [Fact(Skip = "Fails on ADO agent; investigate post-migration.")]
        public async Task SingletonListener_SingletonFunction_ListenerSingletonOverride_InvocationsAreSerialized()
        {
            IHost host = CreateTestJobHost(1);
            await host.StartAsync();

            MethodInfo singletonListenerAndFunctionMethod = typeof(TestJobs).GetMethod("SingletonTriggerJob_SingletonListener_ListenerSingletonOverride");
            await VerifyLeaseState(singletonListenerAndFunctionMethod, SingletonScope.Function, "TestScopeTestValue.Listener", LeaseState.Leased, LeaseStatus.Locked);

            await host.GetJobHost().CallAsync(singletonListenerAndFunctionMethod, new { test = "Test" });

            await host.StopAsync();
            host.Dispose();

            await VerifyLeaseState(singletonListenerAndFunctionMethod, SingletonScope.Function, "TestScope", LeaseState.Available, LeaseStatus.Unlocked);
            await VerifyLeaseState(singletonListenerAndFunctionMethod, SingletonScope.Function, "TestScopeTestValue.Listener", LeaseState.Available, LeaseStatus.Unlocked);
        }

        [Fact(Skip = "Fails on ADO agent; investigate post-migration.")]
        public async Task SingletonTriggerFunction_MultipleConcurrentInvocations_InvocationsAreSerialized()
        {
            IHost host = CreateTestJobHost(1);
            host.Start();

            // trigger a bunch of parallel invocations
            int numMessages = 20;
            List<Task> invokeTasks = new List<Task>();
            JsonSerializer serializer = new JsonSerializer();
            for (int i = 0; i < numMessages; i++)
            {
                int zone = _rand.Next(3) + 1;

                JObject workItem = new JObject
                {
                    { "ID", i + 1 },
                    { "Region", "Central" },
                    { "Zone", zone },
                    { "Category", 3 },
                    { "Description", "Test Work Item " + i }
                };
                await host.GetJobHost().CallAsync(typeof(TestJobs).GetMethod("EnqueueQueue2TestMessage"), new { message = workItem.ToString() });
            }

            // wait for all the messages to be processed by the job
            await TestHelpers.Await(() =>
            {
                return (TestJobs.Queue2MessageCount == numMessages &&
                       TestJobs.JobInvocations.Select(p => p.Value).Sum() == numMessages) || TestJobs.FailureDetected;
            }, pollingInterval: 500);

            Assert.False(TestJobs.FailureDetected);
            Assert.Equal(numMessages, TestJobs.JobInvocations[1]);

            await VerifyLeaseState(typeof(TestJobs).GetMethod("SingletonTriggerJob"), SingletonScope.Function, "Central/1", LeaseState.Available, LeaseStatus.Unlocked);
            await VerifyLeaseState(typeof(TestJobs).GetMethod("SingletonTriggerJob"), SingletonScope.Function, "Central/2", LeaseState.Available, LeaseStatus.Unlocked);
            await VerifyLeaseState(typeof(TestJobs).GetMethod("SingletonTriggerJob"), SingletonScope.Function, "Central/3", LeaseState.Available, LeaseStatus.Unlocked);

            await host.StopAsync();
            host.Dispose();
        }

        [Fact]
        public async Task SingletonFunction_Exception_LeaseReleasedImmediately()
        {
            IHost host = CreateTestJobHost(1);
            host.Start();

            WorkItem workItem = new WorkItem
            {
                ID = 1,
                Region = "Central",
                Zone = 3,
                Category = -1,
                Description = "Test Work Item"
            };

            Exception exception = null;
            MethodInfo method = typeof(TestJobs).GetMethod(nameof(TestJobs.SingletonJob));
            try
            {
                await host.GetJobHost().CallAsync(method, new { workItem = workItem });
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.Equal("Exception while executing function: TestJobs.SingletonJob", exception.Message);
            await VerifyLeaseState(method, SingletonScope.Function, "TestValue", LeaseState.Available, LeaseStatus.Unlocked);

            await host.StopAsync();
            host.Dispose();
        }

        [Fact]
        public async Task QueueFunction_SingletonListener()
        {
            IHost host = CreateTestJobHost(1);
            await host.StartAsync();

            MethodInfo method = typeof(TestJobs).GetMethod(nameof(TestJobs.QueueFunction_SingletonListener));
            await VerifyLeaseState(method, SingletonScope.Function, "Listener", LeaseState.Leased, LeaseStatus.Locked);

            await host.GetJobHost().CallAsync(method, new { message = "{}" });

            await host.StopAsync();
            host.Dispose();

            await VerifyLeaseState(method, SingletonScope.Function, "Listener", LeaseState.Available, LeaseStatus.Unlocked);
        }

        [Fact]
        public async Task SingletonFunction_StorageAccountOverride()
        {
            IHost host = CreateTestJobHost<TestJobs1>(1);
            await host.StartAsync();

            MethodInfo method = typeof(TestJobs1).GetMethod(nameof(TestJobs1.SingletonJob_StorageAccountOverride));

            await host.GetJobHost().CallAsync(method, new { message = "{}" });

            await host.StopAsync();
            host.Dispose();

            // make sure the lease blob was only created in the secondary account
            await VerifyLeaseDoesNotExistAsync(method, SingletonScope.Function, null);
            await VerifyLeaseState(method, SingletonScope.Function, null, LeaseState.Available, LeaseStatus.Unlocked, directory: _secondaryLockDirectoryContainerClient);
        }

        [Fact]
        public async Task SingletonFunction_HostScope()
        {
            IHost host = CreateTestJobHost(1);
            host.Start();

            MethodInfo method = typeof(TestJobs).GetMethod("SingletonJobA_HostScope");
            await host.GetJobHost().CallAsync(method, new { });

            await VerifyLeaseState(method, SingletonScope.Host, "TestValue", LeaseState.Available, LeaseStatus.Unlocked);

            await host.StopAsync();
            host.Dispose();
        }

        [Fact]
        public async Task SingletonFunction_HostScope_InvocationsAreSerialized()
        {
            IHost host = CreateTestJobHost(1);
            host.Start();

            MethodInfo methodA = typeof(TestJobs).GetMethod("SingletonJobA_HostScope");
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(host.GetJobHost().CallAsync(methodA, new { }));
            }

            MethodInfo methodB = typeof(TestJobs).GetMethod("SingletonJobB_HostScope");
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(host.GetJobHost().CallAsync(methodB, new { }));
            }
            await Task.WhenAll(tasks);

            Assert.False(TestJobs.FailureDetected);
            Assert.Equal(10, TestJobs.JobInvocations[1]);

            await VerifyLeaseState(methodA, SingletonScope.Host, "TestValue", LeaseState.Available, LeaseStatus.Unlocked);
            await VerifyLeaseState(methodB, SingletonScope.Host, "TestValue", LeaseState.Available, LeaseStatus.Unlocked);

            await host.StopAsync();
            host.Dispose();
        }

        internal async static Task VerifyLeaseState(MethodInfo method, SingletonScope scope, string scopeId, LeaseState leaseState, LeaseStatus leaseStatus, BlobContainerClient directory = null)
        {
            string lockId = FormatLockId(method, scope, scopeId);

            BlobContainerClient lockDirectory = directory ?? _lockDirectoryContainerClient;
            BlobClient blobClient = lockDirectory.GetBlobClient(lockId);

            var properties = await blobClient.GetPropertiesAsync();
            Assert.Equal(leaseState, properties.Value.LeaseState);
            Assert.Equal(leaseStatus, properties.Value.LeaseStatus);
        }

        internal static async Task VerifyLeaseDoesNotExistAsync(MethodInfo method, SingletonScope scope, string scopeId, BlobContainerClient directory = null)
        {
            string lockId = FormatLockId(method, scope, scopeId);

            BlobContainerClient lockDirectory = directory ?? _lockDirectoryContainerClient;
            BlobClient blobClient = lockDirectory.GetBlobClient(lockId);
            Assert.False(await blobClient.ExistsAsync());
        }

        private static string FormatLockId(MethodInfo method, SingletonScope scope, string scopeId)
        {
            string lockId = string.Empty;
            if (method != null && scope == SingletonScope.Function)
            {
                lockId += string.Format("{0}.{1}", method.DeclaringType.FullName, method.Name);
            }

            if (!string.IsNullOrEmpty(scopeId))
            {
                if (!string.IsNullOrEmpty(lockId))
                {
                    lockId += ".";
                }
                lockId += scopeId;
            }

            lockId = string.Format("locks/{0}/{1}", TestHostId, lockId);

            return lockId;
        }

        public class WorkItem
        {
            public int ID { get; set; }
            public string Region { get; set; }
            public int Zone { get; set; }
            public int Category { get; set; }
            public string Description { get; set; }
        }

        public class TestJobs1
        {
            public TestJobs1(int hostId) { }

            [Singleton(Account = Secondary)]
            [NoAutomaticTrigger]
            public async Task SingletonJob_StorageAccountOverride()
            {
                await VerifyLeaseState(
                    GetType().GetMethod("SingletonJob_StorageAccountOverride"),
                    SingletonScope.Function,
                    null,
                    LeaseState.Leased, LeaseStatus.Locked,
                    _secondaryLockDirectoryContainerClient);

                await Task.Delay(50);
            }
        }

        public class TestJobs
        {
            private const string Secondary = "SecondaryStorage";
            public const string LeaseBlobRootPath = "Microsoft.Azure.WebJobs.Host.EndToEndTests.SingletonEndToEndTests+TestJobs";
            public static int Queue1MessageCount = 0;
            public static int Queue2MessageCount = 0;
            public static bool FailureDetected = false;
            public static Dictionary<int, int> JobInvocations = new Dictionary<int, int>();
            private static Dictionary<string, bool> scopeLocks = new Dictionary<string, bool>();
            private static object syncLock = new object();

            private readonly int _hostId;

            public TestJobs(int hostId)
            {
                _hostId = hostId;
            }

            [Singleton(@"{Region}\{Zone}")]
            public async Task SingletonTriggerJob([QueueTrigger(Queue2Name)] WorkItem workItem)
            {
                await VerifyLeaseState(
                    GetType().GetMethod(nameof(SingletonTriggerJob)),
                    SingletonScope.Function,
                    string.Format("{0}/{1}", workItem.Region, workItem.Zone),
                    LeaseState.Leased, LeaseStatus.Locked);

                // When run concurrently, this job will fail very reliably
                string scope = workItem.Region + workItem.Zone.ToString();
                UpdateScopeLock(scope, true);

                await Task.Delay(50);
                IncrementJobInvocationCount();

                UpdateScopeLock(scope, false);
            }

            [Singleton("%test%")]
            [NoAutomaticTrigger]
            public async Task SingletonJob(WorkItem workItem)
            {
                await VerifyLeaseState(
                    GetType().GetMethod(nameof(SingletonJob)),
                    SingletonScope.Function,
                    "TestValue",
                    LeaseState.Leased, LeaseStatus.Locked);

                if (workItem.Category < 0)
                {
                    throw new Exception("Kaboom!");
                }

                // When run concurrently, this job will fail very reliably
                string scope = "default";
                UpdateScopeLock(scope, true);

                await Task.Delay(50);
                IncrementJobInvocationCount();

                UpdateScopeLock(scope, false);
            }

            internal static int HostLock = 0;

            // This function and the one below it both share the
            // same Host level lock scope
            [Singleton("%test%", SingletonScope.Host)]
            [NoAutomaticTrigger]
            public async Task SingletonJobA_HostScope()
            {
                await HostScopeJobImpl();
            }

            [Singleton("%test%", SingletonScope.Host)]
            [NoAutomaticTrigger]
            public async Task SingletonJobB_HostScope()
            {
                await HostScopeJobImpl();
            }

            private async Task HostScopeJobImpl()
            {
                await VerifyLeaseState(
                    null,
                    SingletonScope.Host,
                    "TestValue",
                    LeaseState.Leased, LeaseStatus.Locked);

                UpdateScopeLock("TestValue", true);

                await Task.Delay(50);
                IncrementJobInvocationCount();

                UpdateScopeLock("TestValue", false);
            }

            // Job with an implicit Singleton lock on the trigger listener
            public async Task TriggerJob_SingletonListener([TestTrigger] string test)
            {
                await VerifyLeaseState(
                    GetType().GetMethod("TriggerJob_SingletonListener"),
                    SingletonScope.Function,
                    "Listener",
                    LeaseState.Leased, LeaseStatus.Locked);

                await Task.Delay(50);
                IncrementJobInvocationCount();
            }

            // Job with BOTH an implicit Singleton lock on the trigger listener as
            // well as a explicit function Singleton. This means that there will only
            // be a single listener running, and also means that individual invocations
            // are also serialized by scope.
            [Singleton("TestScope%test%")]
            public async Task SingletonTriggerJob_SingletonListener([TestTrigger] string test)
            {
                await VerifyLeaseState(
                    GetType().GetMethod("SingletonTriggerJob_SingletonListener"),
                    SingletonScope.Function,
                    "TestScopeTestValue",
                    LeaseState.Leased, LeaseStatus.Locked);

                await VerifyLeaseState(
                    GetType().GetMethod("SingletonTriggerJob_SingletonListener"),
                    SingletonScope.Function,
                    "Listener",
                    LeaseState.Leased, LeaseStatus.Locked);

                await Task.Delay(50);
                IncrementJobInvocationCount();
            }

            // Override the implicit listener Singleton by providing our own
            // Singleton using Mode = Listener.
            [Singleton("TestScope")]
            [Singleton("TestScope%test%", Mode = SingletonMode.Listener)]
            public async Task SingletonTriggerJob_SingletonListener_ListenerSingletonOverride([TestTrigger] string test)
            {
                await VerifyLeaseState(
                    GetType().GetMethod("SingletonTriggerJob_SingletonListener_ListenerSingletonOverride"),
                    SingletonScope.Function,
                    "TestScope",
                    LeaseState.Leased, LeaseStatus.Locked);

                await VerifyLeaseState(
                    GetType().GetMethod("SingletonTriggerJob_SingletonListener_ListenerSingletonOverride"),
                    SingletonScope.Function,
                    "TestScopeTestValue.Listener",
                    LeaseState.Leased, LeaseStatus.Locked);

                await Task.Delay(50);
                IncrementJobInvocationCount();
            }

            [Singleton(Mode = SingletonMode.Listener)]
            public async Task QueueFunction_SingletonListener([QueueTrigger("xyz123")] string message)
            {
                await VerifyLeaseState(
                    GetType().GetMethod(nameof(QueueFunction_SingletonListener)),
                    SingletonScope.Function,
                    "Listener",
                    LeaseState.Leased, LeaseStatus.Locked);

                await Task.Delay(50);
                IncrementJobInvocationCount();
            }

            [NoAutomaticTrigger]
            public void EnqueueQueue1TestMessage(string message,
                [Queue(Queue1Name)] ICollector<string> queueMessages)
            {
                queueMessages.Add(message);
                Interlocked.Increment(ref Queue1MessageCount);
            }

            [NoAutomaticTrigger]
            public void EnqueueQueue2TestMessage(string message,
                [Queue(Queue2Name)] ICollector<string> queueMessages)
            {
                queueMessages.Add(message);
                Interlocked.Increment(ref Queue2MessageCount);
            }

            public static void Reset()
            {
                Queue1MessageCount = 0;
                Queue2MessageCount = 0;
                JobInvocations = new Dictionary<int, int>();
                scopeLocks = new Dictionary<string, bool>();
                FailureDetected = false;
            }

            protected void IncrementJobInvocationCount()
            {
                lock (syncLock)
                {
                    if (!JobInvocations.ContainsKey(_hostId))
                    {
                        JobInvocations[_hostId] = 0;
                    }
                    JobInvocations[_hostId]++;
                }
            }

            protected static void UpdateScopeLock(string scope, bool isLocked)
            {
                bool scopeIsLocked = false;
                if (scopeLocks.TryGetValue(scope, out scopeIsLocked)
                    && scopeIsLocked && isLocked)
                {
                    FailureDetected = true;
                }
                scopeLocks[scope] = isLocked;
            }
        }

        private IHost CreateTestJobHost(int hostId, Action<IHostBuilder> extraConfig = null)
        {
            return CreateTestJobHost<TestJobs>(hostId, extraConfig);
        }

        private IHost CreateTestJobHost<TProg>(int hostId, Action<IHostBuilder> extraConfig = null)
        {
            TestJobActivator activator = new TestJobActivator(hostId);

            var hostBuilder = new HostBuilder()
                .ConfigureDefaultTestHost<TProg>(b =>
                {
                    b.UseHostId(TestHostId);

                    // For Queue and Blob Triggers
                    b.AddAzureStorageBlobs();
                    b.AddAzureStorageQueues();

                    b.AddAzureStorageCoreServices();
                    b.AddExtension<TestTriggerAttributeBindingProvider>();
                })
                .ConfigureTestLogger()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IJobActivator>(activator);
                    services.AddSingleton<INameResolver>(_resolver);
                    services.Configure((QueuesOptions o) => o.MaxPollingInterval = TimeSpan.FromSeconds(2));
                    services.Configure((SingletonOptions o) =>
                    {
                        o.LockAcquisitionTimeout = TimeSpan.FromSeconds(10);
                        o.LockAcquisitionPollingInterval = TimeSpan.FromMilliseconds(500);
                    });
                });

            extraConfig?.Invoke(hostBuilder); // test hook gets final say to replace. 

            IHost host = hostBuilder.Build();

            return host;
        }

        private class TestJobActivator : IJobActivator
        {
            private int _hostId;

            public TestJobActivator(int hostId)
            {
                _hostId = hostId;
            }

            public T CreateInstance<T>()
            {
                return (T)Activator.CreateInstance(typeof(T), _hostId);
            }
        }

        [Binding]
        [AttributeUsage(AttributeTargets.Parameter)]
        public class TestTriggerAttribute : Attribute
        {
            public TestTriggerAttribute()
            {
            }
        }

        /// <summary>
        /// Test trigger binding that applies SingletonAttribute to its listener implementation.
        /// </summary>
        public class TestTriggerAttributeBindingProvider : ITriggerBindingProvider, IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                context
                    .AddBindingRule<TestTriggerAttribute>()
                    .BindToTrigger<string>(this);
            }

            public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
            {
                TestTriggerAttribute attribute = context.Parameter.GetCustomAttributes<TestTriggerAttribute>().SingleOrDefault();
                ITriggerBinding binding = null;

                if (attribute != null)
                {
                    binding = new TestTriggerBinding();
                }

                return Task.FromResult(binding);
            }

            public class TestTriggerBinding : ITriggerBinding
            {
                public Type TriggerValueType
                {
                    get { return typeof(string); }
                }

                public IReadOnlyDictionary<string, Type> BindingDataContract
                {
                    get { return new Dictionary<string, Type>(); }
                }

                public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
                {
                    return Task.FromResult<ITriggerData>(new TestTriggerData());
                }

                public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
                {
                    return Task.FromResult<IListener>(new TestTriggerListener());
                }

                public ParameterDescriptor ToParameterDescriptor()
                {
                    return new ParameterDescriptor();
                }

                [Singleton(Mode = SingletonMode.Listener)]
                public class TestTriggerListener : IListener
                {
                    public static int StartCount = 0;

                    public Task StartAsync(CancellationToken cancellationToken)
                    {
                        Interlocked.Increment(ref StartCount);
                        return Task.FromResult(true);
                    }

                    public Task StopAsync(CancellationToken cancellationToken)
                    {
                        return Task.FromResult(true);
                    }

                    public void Cancel()
                    {
                    }

                    public void Dispose()
                    {
                    }
                }

                private class TestTriggerData : ITriggerData
                {
                    public IValueProvider ValueProvider
                    {
                        get { return new TestValueProvider(); }
                    }

                    public IReadOnlyDictionary<string, object> BindingData
                    {
                        get { return new Dictionary<string, object>(); }
                    }

                    private class TestValueProvider : IValueProvider
                    {
                        public Type Type
                        {
                            get { return typeof(string); }
                        }

                        public Task<object> GetValueAsync()
                        {
                            return Task.FromResult<object>("Test");
                        }

                        public string ToInvokeString()
                        {
                            return "Test";
                        }
                    }
                }
            }
        }

        private class TestNameResolver : RandomNameResolver
        {
            public override string Resolve(string name)
            {
                if (name == "test")
                {
                    return "TestValue";
                }
                return base.Resolve(name);
            }
        }

        private class TestFixture : IAsyncLifetime
        {
            public BlobServiceClient BlobServiceClient;
            public BlobServiceClient SecondaryBlobServiceClient;
            public QueueServiceClient QueueServiceClient;


            public TestFixture()
            {
                // Create a default host since we know that's where the account
                // is coming from
                IHost host = new HostBuilder()
                    .ConfigureDefaultTestHost(b =>
                    {
                        b.AddAzureStorageCoreServices();
                    })
                    .Build();

                BlobServiceClient = TestHelpers.GetTestBlobServiceClient();
                QueueServiceClient = TestHelpers.GetTestQueueServiceClient();

                _lockDirectoryContainerClient = BlobServiceClient.GetBlobContainerClient(HostContainerNames.Hosts);

                SecondaryBlobServiceClient = TestHelpers.GetTestBlobServiceClient($"AzureWebJobs{Secondary}");
                _secondaryLockDirectoryContainerClient = SecondaryBlobServiceClient.GetBlobContainerClient(HostContainerNames.Hosts);
            }

            public Task InitializeAsync() => Task.CompletedTask;

            public async Task DisposeAsync()
            {

                await CleanBlobsAsync(BlobServiceClient);
                await CleanBlobsAsync(SecondaryBlobServiceClient);
                await CleanQueuesAsync();
            }

            private async Task CleanBlobsAsync(BlobServiceClient blobServiceClient)
            {
                if (blobServiceClient != null)
                {
                    var containerClient = blobServiceClient.GetBlobContainerClient(HostContainerNames.Hosts);
                    if (await containerClient.ExistsAsync())
                    {
                        await foreach (var testBlob in containerClient.GetBlobsAsync(prefix: string.Format("locks/{0}", TestHostId)))
                        {
                            try
                            {
                                await containerClient.DeleteBlobAsync(testBlob.Name);
                            }
                            catch (RequestFailedException)
                            {
                                // best effort - might fail if there is an active
                                // lease on the blob
                            }
                        }
                    }
                }
            }

            private async Task CleanQueuesAsync()
            {
                if (QueueServiceClient != null)
                {
                    await foreach (var testQueue in QueueServiceClient.GetQueuesAsync(prefix: TestArtifactsPrefix))
                    {
                        await QueueServiceClient.DeleteQueueAsync(testQueue.Name);
                    }
                }
            }
        }
    }
}
