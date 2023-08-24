// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class DynamicConcurrencyEndToEndTests
    {
        private const string TestHostId = "e2etesthost";

        public DynamicConcurrencyEndToEndTests()
        {
            TestEventSource.Reset();
            TestJobs.InvokeCount = 0;
        }

        [Fact]
        public async Task DynamicConcurrencyEnabled_HighCpu_Throttles()
        {
            string functionName = nameof(TestJobs.ConcurrencyTest_HighCpu);
            string functionId = GetFunctionId(functionName);
            int eventCount = 100;
            AddTestEvents("concurrency-work-items-1", eventCount);

            // force an initial concurrency to ensure throttles are hit relatively quickly
            IHost host = CreateTestJobHost<TestJobs>();
            var concurrencyManager = host.GetServiceOrNull<ConcurrencyManager>();
            int initialConcurrency = 10;
            ApplyTestSnapshot(concurrencyManager, highCpuConcurrency: initialConcurrency);

            host.Start();

            await TestHelpers.Await(() =>
            {
                // wait until we've processed some events and we've throttled down
                var logs = GetConcurrencyLogs(host);
                var concurrencyDecreaseLogs = logs.Where(p => p.FormattedMessage.Contains($"{functionId} Decreasing concurrency")).ToArray();
                var throttleLogs = logs.Where(p => p.Level == LogLevel.Warning &&
                    (p.FormattedMessage.Contains("Host CPU threshold exceeded") || p.FormattedMessage.Contains("thread pool starvation detected"))).ToArray();
                bool complete = TestJobs.InvokeCount > 5 && throttleLogs.Length > 0 && concurrencyDecreaseLogs.Length > 0;

                return Task.FromResult(complete);
            }, timeout: 90 * 1000);

            await host.StopAsync();

            var functionSnapshot = GetFunctionSnapshotOrNull(concurrencyManager, functionName);

            // verify concurrency was limited
            Assert.True(functionSnapshot.Concurrency <= initialConcurrency);

            host.Dispose();
        }

        [Fact]
        public async Task DynamicConcurrencyEnabled_HighMemory_MemoryThrottleEnabled_Throttles()
        {
            string functionName = nameof(TestJobs.ConcurrencyTest_HighMemory);
            string functionId = GetFunctionId(functionName);
            int eventCount = 100;
            AddTestEvents("concurrency-work-items-3", eventCount);

            // enable the memory throttle with a low value so it'll be enabled
            int totalAvailableMemoryBytes = 500 * 1024 * 1024;

            // force an initial concurrency to ensure throttles are hit relatively quickly
            IHost host = CreateTestJobHost<TestJobs>(totalAvailableMemoryBytes: totalAvailableMemoryBytes);
            var concurrencyManager = host.GetServiceOrNull<ConcurrencyManager>();
            int initialConcurrency = 5;
            ApplyTestSnapshot(concurrencyManager, highMemoryConcurrency: initialConcurrency);

            host.Start();

            await TestHelpers.Await(() =>
            {
                // wait until we've processed some events and we've throttled down
                var logs = GetConcurrencyLogs(host);
                var concurrencyDecreaseLogs = logs.Where(p => p.FormattedMessage.Contains($"{functionId} Decreasing concurrency")).ToArray();
                var throttleLogs = logs.Where(p => p.Level == LogLevel.Warning && p.FormattedMessage.Contains("Host memory threshold exceeded")).ToArray();
                bool complete = TestJobs.InvokeCount > 5 && throttleLogs.Length > 0 && concurrencyDecreaseLogs.Length > 0;

                return Task.FromResult(complete);
            }, timeout: 90 * 1000);

            await host.StopAsync();

            host.Dispose();
        }

        [Fact(Skip = "Fails on ADO agent; investigate post-migration.")]
        public async Task DynamicConcurrencyEnabled_HighMemory_MemoryThrottleDisabled_Throttles()
        {
            string functionName = nameof(TestJobs.ConcurrencyTest_HighMemory);
            string functionId = GetFunctionId(functionName);
            int eventCount = 100;
            AddTestEvents("concurrency-work-items-3", eventCount);

            // force an initial concurrency to ensure throttles are hit relatively quickly
            IHost host = CreateTestJobHost<TestJobs>();
            var concurrencyManager = host.GetServiceOrNull<ConcurrencyManager>();
            int initialConcurrency = 15;
            ApplyTestSnapshot(concurrencyManager, highMemoryConcurrency: initialConcurrency);

            host.Start();

            await TestHelpers.Await(() =>
            {
                // wait until we've processed some events and we've throttled down
                // in high memory situations, we see CPU/ThreadStarvation throttles fire
                var logs = GetConcurrencyLogs(host);
                var concurrencyDecreaseLogs = logs.Where(p => p.FormattedMessage.Contains($"{functionId} Decreasing concurrency")).ToArray();
                var throttleLogs = logs.Where(p => p.Level == LogLevel.Warning).ToArray();
                bool complete = throttleLogs.Length > 5 && concurrencyDecreaseLogs.Length > 0;

                return Task.FromResult(complete);
            });

            await host.StopAsync();

            host.Dispose();
        }

        [Fact]
        public async Task DynamicConcurrencyEnabled_Lightweight_NoThrottling()
        {
            FunctionConcurrencySnapshot snapshot = null;
            string functionName = nameof(TestJobs.ConcurrencyTest_Lightweight);
            int targetConcurrency = 10;
            int eventCount = 3000;
            AddTestEvents("concurrency-work-items-2", eventCount);

            IHost host = CreateTestJobHost<TestJobs>();
            await WaitForQuietHostAsync(host);

            host.Start();

            var concurrencyManager = host.GetServiceOrNull<ConcurrencyManager>();

            await TestHelpers.Await(() =>
            {
                // wait until we've increased concurrency several times and we've processed
                // many events
                snapshot = GetFunctionSnapshotOrNull(concurrencyManager, functionName);
                return Task.FromResult(snapshot?.Concurrency >= targetConcurrency && TestJobs.InvokeCount > 250);
            });

            await host.StopAsync();

            var logs = GetConcurrencyLogs(host);
            var warningsOrErrors = logs.Where(p => p.Level > LogLevel.Information).ToArray();

            var functionSnapshot = GetFunctionSnapshotOrNull(concurrencyManager, functionName);

            // verify no warnings/errors and also that we've increased concurrency
            Assert.True(warningsOrErrors.Length == 0, string.Join(Environment.NewLine, warningsOrErrors.Take(5).Select(p => p.FormattedMessage)));
            Assert.True(functionSnapshot.Concurrency >= targetConcurrency);

            host.Dispose();
        }

        [Fact]
        public async Task MultipleFunctionsEnabled_Succeeds()
        {
            // add events for the lightweight and high cpu functions
            AddTestEvents("concurrency-work-items-1", 500);
            AddTestEvents("concurrency-work-items-2", 5000);

            // force an initial concurrency to ensure throttles are hit relatively quickly
            IHost host = CreateTestJobHost<TestJobs>();
            var concurrencyManager = host.GetServiceOrNull<ConcurrencyManager>();
            ApplyTestSnapshot(concurrencyManager, highCpuConcurrency: 10);

            host.Start();

            // just run for a bit to exercise ConcurrencyManager 
            await Task.Delay(TimeSpan.FromSeconds(10));

            await host.StopAsync();

            // verify no errors, and that we do have some throttle warnings
            var logs = GetConcurrencyLogs(host);
            Assert.Empty(logs.Where(p => p.Level == LogLevel.Error));
            Assert.NotEmpty(logs.Where(p => p.Level == LogLevel.Warning));

            // When run for a longer period of time, concurrency for the lightweight function can
            // break away while the heavier function stays limited.
            // That can take longer than we want to allow this test to run for so we can't perform
            // those verification checks here.
            host.Dispose();
        }

        [Fact]
        public async Task SnapshotsEnabled_AppliesSnapshotOnStartup()
        {
            int eventCount = 500;
            AddTestEvents("concurrency-work-items-2", eventCount);

            IHost host = CreateTestJobHost<TestJobs>(snapshotPersistenceEnabled: true);
            await WaitForQuietHostAsync(host);

            var concurrencyManager = host.GetServiceOrNull<ConcurrencyManager>();
            var repository = host.Services.GetServices<IConcurrencyStatusRepository>().Last();
            int initialConcurrency = 50;
            var snapshot = CreateTestSnapshot(lightweightConcurrency: initialConcurrency);
            await repository.WriteAsync(snapshot, CancellationToken.None);

            host.Start();

            await TestHelpers.Await(() =>
            {
                // wait for all events to be processed
                return Task.FromResult(TestJobs.InvokeCount >= eventCount);
            });

            await host.StopAsync();

            // ensure no warnings or errors
            var logs = GetConcurrencyLogs(host);
            var warningsOrErrors = logs.Where(p => p.Level > LogLevel.Information).ToArray();
            Assert.Empty(warningsOrErrors);

            // ensure the snapshot was applied on startup
            string functionId = GetFunctionId(nameof(TestJobs.ConcurrencyTest_Lightweight));
            var log = logs.SingleOrDefault(p => p.FormattedMessage == $"Applying status snapshot for function {functionId} (Concurrency: {initialConcurrency})");
            Assert.NotNull(log);

            host.Dispose();
        }

        private void AddTestEvents(string source, int count)
        {
            List<TestEvent> events = new List<TestEvent>();
            for (int i = 0; i < count; i++)
            {
                events.Add(new TestEvent { ID = Guid.NewGuid(), Data = $"TestEvent{i}" });
            }
            TestEventSource.AddEvents(source, events);
        }

        private async Task WaitForQuietHostAsync(IHost host)
        {
            // some tests are expecting no throttle warnings to happen
            // to make tests more stable, wait until all throttles are
            // disabled before starting the test
            // e.g. a previous test may have driven CPU up so we need to
            // wait for it to drop back down
            var throttleManager = host.GetServiceOrNull<IConcurrencyThrottleManager>();
            await TestHelpers.Await(() =>
            {
                return throttleManager.GetStatus().State == ThrottleState.Disabled;
            });
            host.GetTestLoggerProvider().ClearAllLogMessages();
        }
        private IHost CreateTestJobHost<TProg>(int totalAvailableMemoryBytes = -1, bool snapshotPersistenceEnabled = false, Action<IHostBuilder> extraConfig = null)
        {
            var hostBuilder = new HostBuilder()
                .ConfigureDefaultTestHost<TProg>(b =>
                {
                    b.UseHostId(TestHostId)
                        .AddExtension<TestTriggerAttributeBindingProvider>();

                    b.AddAzureStorageCoreServices();

                    b.Services.AddOptions<ConcurrencyOptions>().Configure(options =>
                    {
                        options.DynamicConcurrencyEnabled = true;
                        options.SnapshotPersistenceEnabled = snapshotPersistenceEnabled;
                        options.MaximumFunctionConcurrency = 500;

                        // configure memory limit if specified
                        // memory throttle is disabled by default unless a value > 0 is specified here
                        if (totalAvailableMemoryBytes > 0)
                        {
                            options.TotalAvailableMemoryBytes = (long)totalAvailableMemoryBytes;
                        }
                    });
                })
                .ConfigureLogging((context, b) =>
                {
                    b.SetMinimumLevel(LogLevel.Information);

                    b.AddFilter((category, level) =>
                    {
                        if (category == LogCategories.Concurrency)
                        {
                            return true;
                        }
                        return level >= LogLevel.Information;
                    });
                });

            extraConfig?.Invoke(hostBuilder);

            IHost host = hostBuilder.Build();

            return host;
        }

        private FunctionConcurrencySnapshot GetFunctionSnapshotOrNull(ConcurrencyManager concurrencyManager, string functionName)
        {
            var snapshot = concurrencyManager.GetSnapshot();

            string functionId = GetFunctionId(functionName);
            snapshot.FunctionSnapshots.TryGetValue(functionId, out FunctionConcurrencySnapshot functionSnapshot);

            return functionSnapshot;
        }

        private HostConcurrencySnapshot CreateTestSnapshot(int lightweightConcurrency = 1, int highCpuConcurrency = 1, int highMemoryConcurrency = 1)
        {
            var snapshot = new HostConcurrencySnapshot
            {
                NumberOfCores = Utility.GetEffectiveCoresCount(),
                FunctionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>()
            };

            snapshot.FunctionSnapshots.Add(GetFunctionId(nameof(TestJobs.ConcurrencyTest_Lightweight)), new FunctionConcurrencySnapshot
            {
                Concurrency = lightweightConcurrency
            });
            snapshot.FunctionSnapshots.Add(GetFunctionId(nameof(TestJobs.ConcurrencyTest_HighCpu)), new FunctionConcurrencySnapshot
            {
                Concurrency = highCpuConcurrency
            });
            snapshot.FunctionSnapshots.Add(GetFunctionId(nameof(TestJobs.ConcurrencyTest_HighMemory)), new FunctionConcurrencySnapshot
            {
                Concurrency = highMemoryConcurrency
            });

            return snapshot;
        }

        private void ApplyTestSnapshot(ConcurrencyManager concurrencyManager, int lightweightConcurrency = 1, int highCpuConcurrency = 1, int highMemoryConcurrency = 1)
        {
            var snapshot = CreateTestSnapshot(lightweightConcurrency, highCpuConcurrency, highMemoryConcurrency);
            concurrencyManager.ApplySnapshot(snapshot);
        }

        private string GetFunctionId(string methodName)
        {
            MethodInfo methodInfo = typeof(TestJobs).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            return $"{methodInfo.DeclaringType.FullName}.{methodInfo.Name}";
        }

        private static LogMessage[] GetConcurrencyLogs(IHost host)
        {
            var logProvider = host.GetTestLoggerProvider();
            var logs = logProvider.GetAllLogMessages().Where(p => p.Category == LogCategories.Concurrency).ToArray();

            return logs;
        }

        /// <summary>
        /// Test in memory event source.
        /// </summary>
        public static class TestEventSource
        {
            public static ConcurrentDictionary<string, ConcurrentQueue<TestEvent>> Events = new ConcurrentDictionary<string, ConcurrentQueue<TestEvent>>(StringComparer.OrdinalIgnoreCase);
            private static object _syncLock = new object();

            public static void AddEvents(string source, List<TestEvent> events)
            {
                var existingEvents = Events.GetOrAdd(source, s =>
                {
                    return new ConcurrentQueue<TestEvent>();
                });

                foreach (var evt in events)
                {
                    existingEvents.Enqueue(evt);
                }
            }

            public static List<TestEvent> GetEvents(string source, int count)
            {
                List<TestEvent> result = new List<TestEvent>();

                if (Events.TryGetValue(source, out ConcurrentQueue<TestEvent> events))
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (events.TryDequeue(out TestEvent evt))
                        {
                            result.Add(evt);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                return result;
            }

            public static void Reset()
            {
                Events.Clear();
            }
        }

        public class TestEvent
        {
            public Guid ID { get; set; }

            public string Data { get; set; }
        }

        /// <summary>
        /// Set of test functions that exercise the various host throttles.
        /// </summary>
        public class TestJobs
        {
            private static readonly Random _rand = new Random();
            public static int InvokeCount = 0;

            public static async Task ConcurrencyTest_HighCpu([TestTrigger("concurrency-work-items-1")] TestEvent evt, ILogger log)
            {
                log.LogInformation($"C# Test trigger function processed: {evt.Data}");

                await GenerateLoadAllCoresAsync();

                Interlocked.Increment(ref InvokeCount);
            }

            public static async Task ConcurrencyTest_Lightweight([TestTrigger("concurrency-work-items-2")] TestEvent evt, ILogger log)
            {
                log.LogInformation($"C# Test trigger function processed: {evt.Data}");

                await Task.Delay(50);

                Interlocked.Increment(ref InvokeCount);
            }

            public static async Task ConcurrencyTest_HighMemory([TestTrigger("concurrency-work-items-3")] TestEvent evt, ILogger log)
            {
                log.LogInformation($"C# Test trigger function processed: {evt.Data}");

                // allocate a large chunk of memory
                int numMBs = _rand.Next(100, 250);
                int numBytes = numMBs * 1024 * 1024;
                byte[] bytes = new byte[numBytes];

                // write to that memory
                _rand.NextBytes(bytes);

                await Task.Delay(_rand.Next(50, 250));

                Interlocked.Increment(ref InvokeCount);
            }

            public static async Task GenerateLoadAllCoresAsync()
            {
                int cores = Utility.GetEffectiveCoresCount();
                List<Task> tasks = new List<Task>();
                for (int i = 0; i < cores; i++)
                {
                    var task = Task.Run(() => GenerateLoad());
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
            }

            public static void GenerateLoad()
            {
                int start = 2000;
                int numPrimes = 200;

                for (int i = start; i < start + numPrimes; i++)
                {
                    FindPrimeNumber(i);
                }
            }

            public static long FindPrimeNumber(int n)
            {
                int count = 0;
                long a = 2;
                while (count < n)
                {
                    long b = 2;
                    int prime = 1; // to check if found a prime
                    while (b * b <= a)
                    {
                        if (a % b == 0)
                        {
                            prime = 0;
                            break;
                        }
                        b++;
                    }
                    if (prime > 0)
                    {
                        count++;
                    }
                    a++;
                }
                return (--a);
            }
        }

        [Binding]
        [AttributeUsage(AttributeTargets.Parameter)]
        public class TestTriggerAttribute : Attribute
        {
            public TestTriggerAttribute(string source)
            {
                Source = source;
            }

            public string Source { get; }
        }

        /// <summary>
        /// Test trigger binding that is Dynamic Concurrency enabled.
        /// </summary>
        public class TestTriggerAttributeBindingProvider : ITriggerBindingProvider, IExtensionConfigProvider
        {
            private readonly ConcurrencyManager _concurrencyManager;

            public TestTriggerAttributeBindingProvider(ConcurrencyManager concurrencyManager)
            {
                _concurrencyManager = concurrencyManager;
            }

            public void Initialize(ExtensionConfigContext context)
            {
                context
                    .AddBindingRule<TestTriggerAttribute>()
                    .BindToTrigger<TestEvent>(this);
            }

            public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
            {
                TestTriggerAttribute attribute = context.Parameter.GetCustomAttributes<TestTriggerAttribute>().SingleOrDefault();
                ITriggerBinding binding = null;

                if (attribute != null)
                {
                    binding = new TestTriggerBinding(attribute.Source, _concurrencyManager);
                }

                return Task.FromResult(binding);
            }

            public class TestTriggerBinding : ITriggerBinding
            {
                private readonly string _source;
                private readonly ConcurrencyManager _concurrencyManager;

                public TestTriggerBinding(string source, ConcurrencyManager concurrencyManager)
                {
                    _source = source;
                    _concurrencyManager = concurrencyManager;
                }

                public Type TriggerValueType
                {
                    get { return typeof(TestEvent); }
                }

                public IReadOnlyDictionary<string, Type> BindingDataContract
                {
                    get { return new Dictionary<string, Type>(); }
                }

                public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
                {
                    TestEvent evt = (TestEvent)value;

                    return Task.FromResult<ITriggerData>(new TestTriggerData(evt));
                }

                public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
                {
                    return Task.FromResult<IListener>(new TestTriggerListener(_concurrencyManager, context.Executor, context.Descriptor, _source));
                }

                public ParameterDescriptor ToParameterDescriptor()
                {
                    return new ParameterDescriptor();
                }

                public class TestTriggerListener : IListener
                {
                    private const int _intervalMS = 50;
                    private readonly Timer _timer;
                    private readonly ITriggeredFunctionExecutor _executor;
                    private readonly ConcurrencyManager _concurrencyManager;
                    private readonly FunctionDescriptor _descriptor;
                    private readonly string _source;
                    private int _pendingInvocationCount;
                    private bool _disposed;

                    public TestTriggerListener(ConcurrencyManager concurrencyManager, ITriggeredFunctionExecutor executor, FunctionDescriptor descriptor, string source)
                    {
                        _concurrencyManager = concurrencyManager;
                        _executor = executor;
                        _descriptor = descriptor;
                        _source = source;
                        _timer = new Timer(OnTimer);
                    }

                    public Task StartAsync(CancellationToken cancellationToken)
                    {
                        _timer.Change(_intervalMS, Timeout.Infinite);
                        return Task.CompletedTask;
                    }

                    public Task StopAsync(CancellationToken cancellationToken)
                    {
                        _timer.Change(Timeout.Infinite, Timeout.Infinite);
                        return Task.CompletedTask;
                    }

                    public void Cancel()
                    {
                        _timer.Change(Timeout.Infinite, Timeout.Infinite);
                    }

                    public void Dispose()
                    {
                        _disposed = true;
                        _timer?.Dispose();
                    }

                    public void OnTimer(object state)
                    {
                        try
                        {
                            if (_disposed)
                            {
                                return;
                            }

                            int currentBatchSize = 32;

                            if (_concurrencyManager.Enabled)
                            {
                                // Demonstrates how a listener integrates with Dynamic Concurrency querying
                                // ConcurrencyManager and limiting the amount of new invocations started.
                                var concurrencyStatus = _concurrencyManager.GetStatus(_descriptor.Id);
                                int availableInvocationCount = concurrencyStatus.GetAvailableInvocationCount(_pendingInvocationCount);
                                currentBatchSize = Math.Min(availableInvocationCount, 32);
                                if (currentBatchSize == 0)
                                {
                                    // if we're not healthy or we're at our limit, we'll wait
                                    // a bit before checking again
                                    _timer.Change(1000, Timeout.Infinite);
                                    return;
                                }
                            }

                            // fetch new work up to the recommended batch size and dispatch invocations
                            var events = TestEventSource.GetEvents(_source, currentBatchSize);
                            bool foundEvent = false;
                            foreach (var evt in events)
                            {
                                foundEvent = true;
                                var tIgnore = ExecuteFunctionAsync(evt);
                            }

                            if (foundEvent)
                            {
                                // we want the next invocation to run right away to ensure we quickly fetch
                                // to our max degree of concurrency (we know there were messages).
                                _timer.Change(0, Timeout.Infinite);
                            }
                            else
                            {
                                // when no events were present we delay before checking again
                                _timer.Change(250, Timeout.Infinite);
                            }
                        }
                        catch
                        {
                            // don't let background exceptions propagate
                        }
                    }

                    private async Task ExecuteFunctionAsync(TestEvent evt)
                    {
                        try
                        {
                            Interlocked.Increment(ref _pendingInvocationCount);

                            await _executor.TryExecuteAsync(new TriggeredFunctionData { TriggerValue = evt }, CancellationToken.None);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _pendingInvocationCount);
                        }
                    }
                }

                private class TestTriggerData : ITriggerData
                {
                    private TestEvent _evt;

                    public TestTriggerData(TestEvent evt)
                    {
                        _evt = evt;
                    }

                    public IValueProvider ValueProvider
                    {
                        get { return new TestValueProvider(_evt); }
                    }

                    public IReadOnlyDictionary<string, object> BindingData
                    {
                        get { return new Dictionary<string, object>(); }
                    }

                    private class TestValueProvider : IValueProvider
                    {
                        private TestEvent _evt;

                        public TestValueProvider(TestEvent evt)
                        {
                            _evt = evt;
                        }

                        public Type Type
                        {
                            get { return typeof(TestEvent); }
                        }

                        public Task<object> GetValueAsync()
                        {
                            return Task.FromResult<object>(_evt);
                        }

                        public string ToInvokeString()
                        {
                            return _evt.ID.ToString();
                        }
                    }
                }
            }
        }
    }
}
