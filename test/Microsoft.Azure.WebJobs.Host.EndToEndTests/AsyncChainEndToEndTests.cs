// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class AsyncChainEndToEndTests : IClassFixture<AsyncChainEndToEndTests.TestFixture>
    {
        private const string TestArtifactsPrefix = "asynce2e";

        private const string ContainerName = TestArtifactsPrefix + "%rnd%";

        private const string NonWebJobsBlobName = "NonWebJobs";
        private const string Blob1Name = "Blob1";
        private const string Blob2Name = "Blob2";

        private const string Queue1Name = TestArtifactsPrefix + "q1%rnd%";
        private const string Queue2Name = TestArtifactsPrefix + "q2%rnd%";
        private const string TestQueueName = TestArtifactsPrefix + "q3%rnd%";

        private const string TriggerDetailsMessageStart = "Trigger Details:";

        private static QueueServiceClient _queueServiceClient;
        private static BlobServiceClient _blobServiceClient;

        private static RandomNameResolver _resolver;
        private readonly IHostBuilder _hostBuilder;
        private static EventWaitHandle _functionCompletedEvent;

        private static string _finalBlobContent;
        private static TimeSpan _timeoutJobDelay;

        private readonly QueueClient _testQueueClient;
        private readonly TestFixture _fixture;

        public AsyncChainEndToEndTests(TestFixture fixture)
        {
            _fixture = fixture;
            _resolver = new RandomNameResolver();

            int processorCount = Extensions.Storage.Utility.GetProcessorCount();

            _hostBuilder = new HostBuilder()
                .ConfigureDefaultTestHost<AsyncChainEndToEndTests>(b =>
                {
                    // Necessary for Blob/Queue bindings
                    b.AddAzureStorageQueues();
                    b.AddAzureStorageBlobs();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<INameResolver>(_resolver);
                    services.Configure<QueuesOptions>(o =>
                    {
                        o.MaxPollingInterval = TimeSpan.FromSeconds(2);
                        o.BatchSize = 16;
                        o.NewBatchThreshold = 8 * processorCount; // workaround for static isDynamicSku and ProcessorCount variables in T2 extensions
                    });
                    services.Configure<FunctionResultAggregatorOptions>(o =>
                    {
                        o.IsEnabled = false;
                    });
                });

            _blobServiceClient = fixture.BlobServiceClient;
            _queueServiceClient = fixture.QueueServiceClient;

            // This is how long the Timeout test functions will wait for cancellation.
            _timeoutJobDelay = TimeSpan.FromSeconds(30);

            string queueName = _resolver.ResolveInString(TestQueueName);
            _testQueueClient = _queueServiceClient.GetQueueClient(queueName);

            var response = _testQueueClient.CreateIfNotExistsAsync().Result;
            if (response == null)
            {
                // Queue already exists, need to clear it
                _testQueueClient.ClearMessagesAsync().Wait();
            }
        }

        [Fact]
        public async Task AsyncChainEndToEnd()
        {
            using (_functionCompletedEvent = new ManualResetEvent(initialState: false))
            {
                TestLoggerProvider loggerProvider = await AsyncChainEndToEndInternal(_hostBuilder);

                string firstQueueName = _resolver.ResolveInString(Queue1Name);
                string secondQueueName = _resolver.ResolveInString(Queue2Name);
                string blobContainerName = _resolver.ResolveInString(ContainerName);

                string[] loggerOutputLines = loggerProvider.GetAllLogMessages()
                    .Where(p => p.FormattedMessage != null)
                    .Where(p => p.Category != "Azure.Core")
                    .SelectMany(p => p.FormattedMessage.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
                    .OrderBy(p => p)
                    .ToArray();

                int processorCount = Extensions.Storage.Utility.GetProcessorCount();

                string[] expectedOutputLines = new string[]
                {
                    "Found the following functions:",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.WriteStartDataMessageToQueue",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.QueueToQueueAsync",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.QueueToBlobAsync",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.AlwaysFailJob",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.DisabledJob",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.TimeoutJob",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.TimeoutJob_NoAttribute",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.TimeoutJob_Throw",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.TimeoutJob_Throw_NoToken",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.BlobToBlobAsync",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.ReadResultBlob",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.SystemParameterBindingOutput",
                    "Function 'AsyncChainEndToEndTests.DisabledJob' is disabled",
                    "Job host started",
                    "Executing 'AsyncChainEndToEndTests.WriteStartDataMessageToQueue' (Reason='This function was programmatically called via the host APIs.', Id=",
                    "Trigger Details: MessageId: ",
                    "Executed 'AsyncChainEndToEndTests.WriteStartDataMessageToQueue' (Succeeded, Id=",
                    $"Executing 'AsyncChainEndToEndTests.QueueToQueueAsync' (Reason='New queue message detected on '{firstQueueName}'.', Id=",
                    "Trigger Details: MessageId: ",
                    "Executed 'AsyncChainEndToEndTests.QueueToQueueAsync' (Succeeded, Id=",
                    $"Executing 'AsyncChainEndToEndTests.QueueToBlobAsync' (Reason='New queue message detected on '{secondQueueName}'.', Id=",
                    "Executed 'AsyncChainEndToEndTests.QueueToBlobAsync' (Succeeded, Id=",
                    $"Executing 'AsyncChainEndToEndTests.BlobToBlobAsync' (Reason='New blob detected(LogsAndContainerScan): {blobContainerName}/Blob1', Id=",
                    "Executed 'AsyncChainEndToEndTests.BlobToBlobAsync' (Succeeded, Id=",
                    "Job host stopped",
                    "Executing 'AsyncChainEndToEndTests.ReadResultBlob' (Reason='This function was programmatically called via the host APIs.', Id=",
                    "Executed 'AsyncChainEndToEndTests.ReadResultBlob' (Succeeded, Id=",
                    "Trigger Details: MessageId: ",
                    "User ILogger log",
                    "User TraceWriter log 1",
                    "User TraceWriter log 2",
                    "Starting JobHost",
                    "Stopped the listener 'Microsoft.Azure.WebJobs.Extensions.Storage.Common.Listeners.CompositeListener' for function 'BlobToBlobAsync'",
                    "Stopped the listener 'Microsoft.Azure.WebJobs.Extensions.Storage.Common.Listeners.QueueListener' for function 'QueueToBlobAsync'",
                    "Stopped the listener 'Microsoft.Azure.WebJobs.Extensions.Storage.Common.Listeners.QueueListener' for function 'QueueToQueueAsync'",
                    "Stopping JobHost",
                    "Stopping the listener 'Microsoft.Azure.WebJobs.Extensions.Storage.Common.Listeners.CompositeListener' for function 'BlobToBlobAsync'",
                    "Stopping the listener 'Microsoft.Azure.WebJobs.Extensions.Storage.Common.Listeners.QueueListener' for function 'QueueToBlobAsync'",
                    "Stopping the listener 'Microsoft.Azure.WebJobs.Extensions.Storage.Common.Listeners.QueueListener' for function 'QueueToQueueAsync'",
                    "registered http endpoint", // logged in Microsoft.Azure.WebJobs.Extensions.Blobs.BlobsExtensionConfigProvider
                    "QueuesOptions",
                    "{",
                    "  \"BatchSize\": 16",
                    "  \"MaxDequeueCount\": 5,",
                    "  \"MaxPollingInterval\": \"00:00:02\",",
                    string.Format("  \"NewBatchThreshold\": {0},", 8 * processorCount),
                    "  \"VisibilityTimeout\": \"00:00:00\"",
                    "  \"MessageEncoding\": \"Base64\"",
                    "}",
                    "LoggerFilterOptions",
                    "{",
                    "  \"MinLevel\": \"Information\"",
                    "  \"Rules\": []",
                    "}",
                    "FunctionResultAggregatorOptions",
                    "{",
                    "  \"BatchSize\": 1000",
                    "  \"FlushTimeout\": \"00:00:30\",",
                    "  \"IsEnabled\": false",
                    "}",
                    "ConcurrencyOptions",
                    "{",
                    "  \"DynamicConcurrencyEnabled\": false",
                    "  \"MaximumFunctionConcurrency\": 500",
                    "  \"CPUThreshold\": 0.8",
                    "  \"SnapshotPersistenceEnabled\": true",
                    "}",
                    "BlobsOptions",
                    "{",
                    string.Format("  \"MaxDegreeOfParallelism\": {0}", 8 * processorCount),
                    "}",
                    "QueuesOptions", // This QueuesOptions are an internal type within Microsoft.Azure.WebJobs.Extensions.Storage.Blobs
                    "{",
                    "  \"BatchSize\": 16",
                    string.Format("  \"NewBatchThreshold\": {0},", 8 * processorCount),
                    "  \"MaxPollingInterval\": \"00:01:00\",",
                    "  \"MaxDequeueCount\": 5,",
                    "  \"VisibilityTimeout\": \"00:00:00\"",
                    "  \"MessageEncoding\": \"Base64\"",
                    "}",
                    "SingletonOptions",
                    "{",
                    "  \"ListenerLockPeriod\": \"00:01:00\"",
                    "  \"ListenerLockRecoveryPollingInterval\": \"00:01:00\"",
                    "  \"LockAcquisitionPollingInterval\": \"00:00:05\"",
                    "  \"LockAcquisitionTimeout\": \"",
                    "  \"LockPeriod\": \"00:00:15\"",
                    "}",
                }.OrderBy(p => p).ToArray();

                bool hasError = loggerOutputLines.Any(p => p.Contains("Function had errors"));
                Assert.False(hasError);

                // Validate logger output
                for (int i = 0; i < expectedOutputLines.Length; i++)
                {
                    Assert.StartsWith(expectedOutputLines[i], loggerOutputLines[i]);
                }

                // Verify that trigger details are properly formatted
                string[] triggerDetailsLoggerOutput = loggerOutputLines
                    .Where(m => m.StartsWith(TriggerDetailsMessageStart)).ToArray();

                string expectedPattern = "Trigger Details: MessageId: (.*), DequeueCount: [0-9]+, InsertedOn: (.*)";

                foreach (string msg in triggerDetailsLoggerOutput)
                {
                    Assert.True(Regex.IsMatch(msg, expectedPattern), $"Expected trace event {expectedPattern} not found.");
                }
            }
        }

        [Fact]
        public async Task AsyncChainEndToEnd_CustomFactories()
        {
            using (_functionCompletedEvent = new ManualResetEvent(initialState: false))
            {
                TestQueueProcessorFactory queueProcessorFactory = new TestQueueProcessorFactory();

                _hostBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<IQueueProcessorFactory>(queueProcessorFactory);
                });

                await AsyncChainEndToEndInternal(_hostBuilder);

                Assert.Equal(2, queueProcessorFactory.TestQueueProcessors.Count);
                Assert.True(queueProcessorFactory.TestQueueProcessors.All(p => p.Context.Queue.Name.StartsWith("asynce2eq")));
                Assert.True(queueProcessorFactory.TestQueueProcessors.Sum(p => p.BeginProcessingCount) >= 2);
                Assert.True(queueProcessorFactory.TestQueueProcessors.Sum(p => p.CompleteProcessingCount) >= 2);
            }
        }

        [Fact]
        public async Task LoggerLogging()
        {
            using (_functionCompletedEvent = new ManualResetEvent(initialState: false))
            {
                TestLoggerProvider loggerProvider = await AsyncChainEndToEndInternal(_hostBuilder);

                // Validate Logger
                bool hasError = loggerProvider.GetAllLogMessages().Where(p => p.FormattedMessage != null && p.Category != "Azure.Core" && p.FormattedMessage.Contains("Error")).Any();
                Assert.False(hasError);

                IEnumerable<string> userLogMessages = loggerProvider.GetAllLogMessages()
                    .Where(p => p.Category.StartsWith("Function.") && p.Category.EndsWith(".User"))
                    .Select(p => p.FormattedMessage);

                Assert.Equal(3, userLogMessages.Count());
                Assert.NotNull(userLogMessages.SingleOrDefault(p => p == "User TraceWriter log 1"));
                Assert.NotNull(userLogMessages.SingleOrDefault(p => p == "User TraceWriter log 2"));
                Assert.NotNull(userLogMessages.SingleOrDefault(p => p == "User ILogger log"));
            }
        }

        [Fact]
        public async Task AggregatorAndEventCollector()
        {
            using (_functionCompletedEvent = new ManualResetEvent(initialState: false))
            {
                var eventCollectorProvider = new TestEventCollectorProvider();

                // enable the aggregator
                _hostBuilder.ConfigureServices(services =>
                {
                    services.Configure((FunctionResultAggregatorOptions o) =>
                    {
                        o.IsEnabled = true;
                        o.BatchSize = 1;
                    });

                    services.AddSingleton<IEventCollectorProvider>(eventCollectorProvider);
                });

                IHost host = _hostBuilder.Build();

                JobHost jobHost = host.GetJobHost();

                await jobHost.StartAsync();
                await jobHost.CallAsync(typeof(AsyncChainEndToEndTests).GetMethod(nameof(WriteStartDataMessageToQueue)));

                var loggerProvider = host.GetTestLoggerProvider();
                await WaitForFunctionCompleteAsync(loggerProvider);

                // ensure all logs have had a chance to flush
                await Task.Delay(3000);

                await host.StopAsync();

                // Make sure the aggregator was logged to
                var logger = loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Aggregator).Single();
                var count = logger.GetLogMessages().Count;
                Assert.True(count == 4, $"Expected 4. Actual {count}.{Environment.NewLine}{loggerProvider.GetLogString()}");

                // Make sure the eventCollector was logged 
                eventCollectorProvider.EventCollector.AssertFunctionCount(4, loggerProvider.GetLogString());

                host.Dispose();
            }
        }

        [Fact]
        public async Task AggregatorOnly()
        {
            using (_functionCompletedEvent = new ManualResetEvent(initialState: false))
            {
                // enable the aggregator
                _hostBuilder.ConfigureServices(services =>
                {
                    services.Configure<FunctionResultAggregatorOptions>(o =>
                    {
                        o.IsEnabled = true;
                        o.BatchSize = 1;
                    });
                });

                IHost host = _hostBuilder.Build();
                var jobHost = host.GetJobHost<AsyncChainEndToEndTests>();

                await host.StartAsync();
                await jobHost.CallAsync(nameof(WriteStartDataMessageToQueue));

                var loggerProvider = host.GetTestLoggerProvider();
                await WaitForFunctionCompleteAsync(loggerProvider);

                // ensure all logs have had a chance to flush
                await Task.Delay(3000);

                await host.StopAsync();

                // Make sure the aggregator was logged to

                var logger = loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Aggregator).Single();
                var count = logger.GetLogMessages().Count;
                Assert.True(count == 4, $"Expected 4. Actual {count}.{Environment.NewLine}{loggerProvider.GetLogString()}");
                host.Dispose();
            }
        }

        [Fact]
        public async Task EventCollectorOnly()
        {
            using (_functionCompletedEvent = new ManualResetEvent(initialState: false))
            {
                // aggregator is disabled by default in these tests

                // add a FunctionEventCollector
                var eventCollector = new TestFunctionEventCollector();

                _hostBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<IAsyncCollector<FunctionInstanceLogEntry>>(eventCollector);
                });

                IHost host = _hostBuilder.Build();
                JobHost jobHost = host.GetJobHost();

                await jobHost.StartAsync();
                await jobHost.CallAsync(typeof(AsyncChainEndToEndTests).GetMethod("WriteStartDataMessageToQueue"));

                var loggerProvider = host.GetTestLoggerProvider();
                await WaitForFunctionCompleteAsync(loggerProvider);

                // ensure all logs have had a chance to flush
                await Task.Delay(3000);

                await jobHost.StopAsync();

                // Make sure the aggregator was logged to
                var logger = host.GetTestLoggerProvider().CreatedLoggers.Where(l => l.Category == LogCategories.Aggregator).SingleOrDefault();
                Assert.Null(logger);

                // Make sure the eventCollector was logged
                eventCollector.AssertFunctionCount(4, loggerProvider.GetLogString());
            }
        }

        [Fact]
        public async Task FunctionFailures_LogsExpectedMessage()
        {
            IHost host = _hostBuilder.Build();
            JobHost jobHost = host.GetJobHost();

            MethodInfo methodInfo = GetType().GetMethod(nameof(AlwaysFailJob));
            try
            {
                await jobHost.CallAsync(methodInfo);
            }
            catch { }

            string expectedName = $"{methodInfo.DeclaringType.FullName}.{methodInfo.Name}";

            // Validate Logger
            // Logger only writes out a single log message (which includes the Exception).        
            var logger = host.GetTestLoggerProvider().CreatedLoggers.Where(l => l.Category == LogCategories.Results).Single();
            var logMessage = logger.GetLogMessages().Single();
            var loggerException = logMessage.Exception as FunctionException;
            Assert.NotNull(loggerException);
            Assert.Equal(expectedName, loggerException.MethodName);
        }

        [Fact]
        public async Task SystemParameterBindingOutput_GeneratesExpectedBlobs()
        {
            IHost host = _hostBuilder.Build();
            JobHost jobHost = host.GetJobHost();

            var blobContainerClient = _blobServiceClient.GetBlobContainerClient("test-output");
            if (await blobContainerClient.ExistsAsync())
            {
                await foreach (var blob in blobContainerClient.GetBlobsAsync())
                {
                    await blobContainerClient.DeleteBlobIfExistsAsync(blob.Name);
                }
            }

            MethodInfo methodInfo = GetType().GetMethod("SystemParameterBindingOutput");
            var arguments = new Dictionary<string, object>
            {
                { "input", "Test Value" }
            };
            await jobHost.CallAsync(methodInfo, arguments);

            // We expect 3 separate blobs to have been written
            int blobCount = 0;
            await foreach (var blob in blobContainerClient.GetBlobsAsync())
            {
                string content = await blobContainerClient.GetBlobClient(blob.Name).DownloadTextAsync();
                Assert.Equal("Test Value", content.Trim(new char[] { '\uFEFF', '\u200B' }));

                blobCount++;
            }

            Assert.Equal(3, blobCount);

            // Test Cleanup
            if (await blobContainerClient.ExistsAsync())
            {
                await foreach (var blob in blobContainerClient.GetBlobsAsync())
                {
                    await blobContainerClient.DeleteBlobIfExistsAsync(blob.Name);
                }
            }
        }

        [Fact]
        public async Task Timeout_TimeoutExpires_Cancels()
        {
            _hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<IWebJobsExceptionHandlerFactory, CapturingExceptionHandlerFactory>();
            });

            IHost host = _hostBuilder.Build();

            await RunTimeoutTest(host, typeof(TaskCanceledException), nameof(TimeoutJob));

            var exceptionHandler = host.Services.GetService<IWebJobsExceptionHandler>() as CapturingExceptionHandler;
            Assert.Empty(exceptionHandler.UnhandledExceptionInfos);
            Assert.Empty(exceptionHandler.TimeoutExceptionInfos);
        }

        [Fact]
        public async Task TimeoutWithThrow_TimeoutExpires_CancelsAndThrows()
        {
            _hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<IWebJobsExceptionHandlerFactory, CapturingExceptionHandlerFactory>();
            });

            IHost host = _hostBuilder.Build();
            await RunTimeoutTest(host, typeof(FunctionTimeoutException), nameof(TimeoutJob_Throw));

            var exceptionHandler = host.Services.GetService<IWebJobsExceptionHandler>() as CapturingExceptionHandler;
            var exception = exceptionHandler.TimeoutExceptionInfos.Single().SourceException;
            Assert.IsType<FunctionTimeoutException>(exception);
            Assert.Empty(exceptionHandler.UnhandledExceptionInfos);
        }

        [Fact]
        public async Task TimeoutWithThrow_NoCancellationToken_CancelsAndThrows()
        {
            _hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<IWebJobsExceptionHandlerFactory, CapturingExceptionHandlerFactory>();
            });

            IHost host = _hostBuilder.Build();
            await RunTimeoutTest(host, typeof(FunctionTimeoutException), nameof(TimeoutJob_Throw_NoToken));

            var exceptionHandler = host.Services.GetService<IWebJobsExceptionHandler>() as CapturingExceptionHandler;
            var exception = exceptionHandler.TimeoutExceptionInfos.Single().SourceException;
            Assert.IsType<FunctionTimeoutException>(exception);
            Assert.Empty(exceptionHandler.UnhandledExceptionInfos);
        }

        [Fact]
        public async Task Timeout_UsingOptions()
        {
            _hostBuilder.ConfigureServices(services =>
            {
                services.Configure<JobHostFunctionTimeoutOptions>(o =>
                {
                    o.Timeout = TimeSpan.FromSeconds(1);
                    o.TimeoutWhileDebugging = true;
                    o.ThrowOnTimeout = true;
                });

                services.AddSingleton<IWebJobsExceptionHandlerFactory, CapturingExceptionHandlerFactory>();
            });

            IHost host = _hostBuilder.Build();
            await RunTimeoutTest(host, typeof(FunctionTimeoutException), nameof(TimeoutJob_NoAttribute));

            var exceptionHandler = host.Services.GetService<IWebJobsExceptionHandler>() as CapturingExceptionHandler;
            var exception = exceptionHandler.TimeoutExceptionInfos.Single().SourceException;
            Assert.IsType<FunctionTimeoutException>(exception);
            Assert.Empty(exceptionHandler.UnhandledExceptionInfos);
        }

        private async Task RunTimeoutTest(IHost host, Type expectedExceptionType, string functionName)
        {
            JobHost jobHost = host.GetJobHost();

            try
            {
                await jobHost.StartAsync();

                MethodInfo methodInfo = GetType().GetMethod(functionName);

                Exception ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
                {
                    await jobHost.CallAsync(methodInfo);
                });

                Assert.IsType(expectedExceptionType, ex);
            }
            finally
            {
                await jobHost.StopAsync();
            }

            string expectedExceptionMessage = $"Timeout value of 00:00:01 exceeded by function 'AsyncChainEndToEndTests.{functionName}'";
            string expectedResultMessage = $"Executed 'AsyncChainEndToEndTests.{functionName}' (Failed, Id=";

            // Validate Logger
            var loggerProvider = host.GetTestLoggerProvider();
            var resultLogger = loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Results).Single();
            var executorLogger = loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.CreateFunctionCategory(functionName)).Single();

            // Results logs the exception with no message
            Assert.NotNull(resultLogger.GetLogMessages().Single().Exception);
            Assert.Null(resultLogger.GetLogMessages().Single().FormattedMessage);

            // It logs Executed/Executing messages and the timeout message.
            Assert.Equal(3, executorLogger.GetLogMessages().Count());

            executorLogger.GetLogMessages().Single(p => p.FormattedMessage != null && p.FormattedMessage.StartsWith(expectedExceptionMessage));
            LogMessage resultMessage = executorLogger.GetLogMessages().Single(p => p.FormattedMessage != null && p.FormattedMessage.StartsWith(expectedResultMessage));
            Assert.NotNull(resultMessage.Exception);
        }

        [Fact]
        public async Task Timeout_NoExpiry_CompletesSuccessfully()
        {
            IHost host = _hostBuilder.Build();
            JobHost jobHost = host.GetJobHost();

            _timeoutJobDelay = TimeSpan.FromSeconds(0);
            MethodInfo methodInfo = GetType().GetMethod(nameof(TimeoutJob));
            await jobHost.CallAsync(methodInfo);

            // Validate Logger
            LogMessage[] logErrors = host.GetTestLoggerProvider().GetAllLogMessages().Where(l => l.Level == Microsoft.Extensions.Logging.LogLevel.Error).ToArray();
            Assert.Empty(logErrors);
        }

        [NoAutomaticTrigger]
        public static void WriteStartDataMessageToQueue(
            [Queue(Queue1Name)] out string queueMessage,
            [Blob(ContainerName + "/" + NonWebJobsBlobName, FileAccess.Write)] Stream nonSdkBlob)
        {
            queueMessage = " works";

            byte[] messageBytes = Encoding.UTF8.GetBytes("async");
            nonSdkBlob.Write(messageBytes, 0, messageBytes.Length);
        }

        [NoAutomaticTrigger]
        public static void AlwaysFailJob()
        {
            throw new Exception("Kaboom!");
        }

        [NoAutomaticTrigger]
        public static void SystemParameterBindingOutput(
            [QueueTrigger("test")] string input,
            [Blob("test-output/{rand-guid}")] out string blob,
            [Blob("test-output/{rand-guid:N}")] out string blob2,
            [Blob("test-output/{datetime:yyyy-mm-dd}:{rand-guid:N}")] out string blob3)
        {
            blob = blob2 = blob3 = input;
        }

        [Disable("Disable_DisabledJob")]
        public static void DisabledJob([QueueTrigger(Queue1Name)] string message)
        {
        }

        [NoAutomaticTrigger]
        [Timeout("00:00:01", TimeoutWhileDebugging = true)]
        public static async Task TimeoutJob(CancellationToken cancellationToken, TraceWriter log)
        {
            log.Info("Started");
            await Task.Delay(_timeoutJobDelay, cancellationToken);
            log.Info("Completed");
        }

        [NoAutomaticTrigger]
        [Timeout("00:00:01", ThrowOnTimeout = true, TimeoutWhileDebugging = true)]
        public static async Task TimeoutJob_Throw(CancellationToken cancellationToken, TraceWriter log)
        {
            log.Info("Started");
            await Task.Delay(_timeoutJobDelay, cancellationToken);
            log.Info("Completed");
        }

        [NoAutomaticTrigger]
        [Timeout("00:00:01", ThrowOnTimeout = true, TimeoutWhileDebugging = true)]
        public static async Task TimeoutJob_Throw_NoToken(TraceWriter log)
        {
            log.Info("Started");
            await Task.Delay(_timeoutJobDelay);
            log.Info("Completed");
        }

        [NoAutomaticTrigger]
        public static async Task TimeoutJob_NoAttribute(CancellationToken cancellationToken, TraceWriter log)
        {
            log.Info("Started");
            await Task.Delay(_timeoutJobDelay, cancellationToken);
            log.Info("Completed");
        }


        public static async Task QueueToQueueAsync(
            [QueueTrigger(Queue1Name)] string message,
            [Queue(Queue2Name)] IAsyncCollector<string> output,
            CancellationToken token,
            TraceWriter trace)
        {
            var content = await _blobServiceClient.GetBlobContainerClient(_resolver.ResolveInString(ContainerName)).GetBlobClient(NonWebJobsBlobName).DownloadTextAsync(cancellationToken: token);
            trace.Info("User TraceWriter log 1");

            await output.AddAsync(content + message);
        }

        public static async Task QueueToBlobAsync(
            [QueueTrigger(Queue2Name)] string message,
            [Blob(ContainerName + "/" + Blob1Name, FileAccess.Write)] Stream blobStream,
            CancellationToken token,
            TraceWriter trace,
            ILogger logger)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            trace.Info($"User TraceWriter log 2");
            logger.LogInformation("User ILogger log");

            await blobStream.WriteAsync(messageBytes, 0, messageBytes.Length);
        }

        public static async Task BlobToBlobAsync(
            [BlobTrigger(ContainerName + "/" + Blob1Name)] Stream inputStream,
            string blobTrigger,
            Uri uri,
            IDictionary<string, string> metadata,
            BlobProperties properties,
            [Blob(ContainerName + "/" + Blob2Name, FileAccess.Write)] Stream outputStream,
            CancellationToken token)
        {
            Assert.True(uri.ToString().EndsWith(blobTrigger));
            string parentId = metadata["AzureWebJobsParentId"];
            Assert.True(Guid.TryParse(parentId, out Guid g));
            Assert.Equal("application/octet-stream", properties.ContentType);

            // Should not be signaled
            if (token.IsCancellationRequested)
            {
                _functionCompletedEvent.Set();
                return;
            }

            await inputStream.CopyToAsync(outputStream);
            outputStream.Close();

            _functionCompletedEvent.Set();
        }

        public static void ReadResultBlob(
            [Blob(ContainerName + "/" + Blob2Name)] string blob,
            CancellationToken token)
        {
            // Should not be signaled
            if (token.IsCancellationRequested)
            {
                return;
            }

            _finalBlobContent = blob;
        }

        private async Task<TestLoggerProvider> AsyncChainEndToEndInternal(IHostBuilder hostBuilder)
        {
            using (IHost host = hostBuilder.Build())
            {
                JobHost jobHost = host.GetJobHost();

                await host.StartAsync();

                IHostIdProvider idProvider = host.Services.GetService<IHostIdProvider>();
                string hostId = await idProvider.GetHostIdAsync(CancellationToken.None);

                Assert.NotEmpty(hostId);

                await jobHost.CallAsync(typeof(AsyncChainEndToEndTests).GetMethod(nameof(WriteStartDataMessageToQueue)));

                var loggerProvider = host.GetTestLoggerProvider();
                await WaitForFunctionCompleteAsync(loggerProvider);

                await jobHost.CallAsync(typeof(AsyncChainEndToEndTests).GetMethod(nameof(ReadResultBlob)));
                Assert.Equal("async works", _finalBlobContent);

                await host.StopAsync();

                return loggerProvider;
            }
        }

        private async Task WaitForFunctionCompleteAsync(TestLoggerProvider loggerProvider, int waitTimeout = 30000)
        {
            await TestHelpers.Await(() => _functionCompletedEvent.WaitOne(200), timeout: waitTimeout, pollingInterval: 500,
                userMessageCallback: () => $"Function did not complete in {waitTimeout} ms. Current time: {DateTime.UtcNow.ToString("HH:mm:ss.fff")}{Environment.NewLine}{loggerProvider.GetLogString()}");
        }

        private class TestQueueProcessorFactory : IQueueProcessorFactory
        {
            public List<TestQueueProcessor> TestQueueProcessors = new List<TestQueueProcessor>();

            public QueueProcessor Create(QueueProcessorOptions context)
            {
                var queueProcessor = new TestQueueProcessor(context);
                TestQueueProcessors.Add(queueProcessor);
                return queueProcessor;
            }
        }

        private class TestQueueProcessor : QueueProcessor
        {
            public int BeginProcessingCount = 0;
            public int CompleteProcessingCount = 0;

            public TestQueueProcessor(QueueProcessorOptions context)
                : base(context)
            {
                Context = context;
            }

            internal QueueProcessorOptions Context { get; private set; }

            protected override Task<bool> BeginProcessingMessageAsync(QueueMessage message, CancellationToken cancellationToken)
            {
                BeginProcessingCount++;
                return base.BeginProcessingMessageAsync(message, cancellationToken);
            }

            protected override Task CompleteProcessingMessageAsync(QueueMessage message, FunctionResult result, CancellationToken cancellationToken)
            {
                CompleteProcessingCount++;
                return base.CompleteProcessingMessageAsync(message, result, cancellationToken);
            }

            protected override async Task ReleaseMessageAsync(QueueMessage message, FunctionResult result, TimeSpan visibilityTimeout, CancellationToken cancellationToken)
            {
                // demonstrates how visibility timeout for failed messages can be customized
                // the logic here could implement exponential backoff, etc.
                visibilityTimeout = TimeSpan.FromSeconds(message.DequeueCount);

                await base.ReleaseMessageAsync(message, result, visibilityTimeout, cancellationToken);
            }
        }

        public class TestFixture : IAsyncLifetime
        {
            public readonly BlobServiceClient BlobServiceClient;
            public readonly QueueServiceClient QueueServiceClient;

            public TestFixture()
            {
                // We know the tests are using the default storage provider, so pull that out
                // of a default host.
                IHost host = new HostBuilder()
                    .ConfigureDefaultTestHost<TestFixture>(b =>
                    {
                        // Necessary for Blob/Queue bindings
                        b.AddAzureStorageBlobs();
                        b.AddAzureStorageQueues();
                    })
                    .Build();

                BlobServiceClient = TestHelpers.GetTestBlobServiceClient();
                QueueServiceClient = TestHelpers.GetTestQueueServiceClient();
            }

            public Task InitializeAsync() => Task.CompletedTask;

            public async Task DisposeAsync()
            {
                await CleanBlobsAsync();
                await CleanQueuesAsync();
            }

            private async Task CleanBlobsAsync()
            {
                if (BlobServiceClient != null)
                {
                    await foreach (var testBlob in BlobServiceClient.GetBlobContainersAsync(prefix: TestArtifactsPrefix))
                    {
                        await BlobServiceClient.DeleteBlobContainerAsync(testBlob.Name);
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

        private class CapturingExceptionHandlerFactory : IWebJobsExceptionHandlerFactory
        {
            private readonly CapturingExceptionHandler _handler = new CapturingExceptionHandler();

            public IWebJobsExceptionHandler Create(IHost host) => _handler;
        }

        private class CapturingExceptionHandler : IWebJobsExceptionHandler
        {
            public ICollection<ExceptionDispatchInfo> UnhandledExceptionInfos { get; } = new List<ExceptionDispatchInfo>();
            public ICollection<ExceptionDispatchInfo> TimeoutExceptionInfos { get; } = new List<ExceptionDispatchInfo>();

            public Task OnTimeoutExceptionAsync(ExceptionDispatchInfo exceptionInfo, TimeSpan timeoutGracePeriod)
            {
                TimeoutExceptionInfos.Add(exceptionInfo);
                return Task.FromResult(0);
            }

            public Task OnUnhandledExceptionAsync(ExceptionDispatchInfo exceptionInfo)
            {
                // TODO: FACAVAL - Validate this, tests are stepping over each other.
                if (!(exceptionInfo.SourceException is RequestFailedException storageException &&
                    storageException?.InnerException is TaskCanceledException))
                {
                    UnhandledExceptionInfos.Add(exceptionInfo);
                }
                return Task.FromResult(0);
            }
        }

        private class TestEventCollectorProvider : IEventCollectorProvider
        {
            public TestFunctionEventCollector EventCollector = new TestFunctionEventCollector();

            public IAsyncCollector<FunctionInstanceLogEntry> Create()
            {
                return EventCollector;
            }
        }

        private class TestFunctionEventCollector : IAsyncCollector<FunctionInstanceLogEntry>
        {
            private List<FunctionInstanceLogEntry> LogEntries { get; } = new List<FunctionInstanceLogEntry>();

            public Dictionary<Guid, StringBuilder> _state = new Dictionary<Guid, StringBuilder>();

            public Task AddAsync(FunctionInstanceLogEntry item, CancellationToken cancellationToken = default(CancellationToken))
            {
                if (!_state.TryGetValue(item.FunctionInstanceId, out StringBuilder prevState))
                {
                    prevState = new StringBuilder();
                    _state[item.FunctionInstanceId] = prevState;
                }
                if (item.IsStart)
                {
                    prevState.Append("[start]");
                }
                if (item.IsPostBind)
                {
                    prevState.Append("[postbind]");
                }
                if (item.IsCompleted)
                {
                    prevState.Append("[complete]");
                }

                LogEntries.Add(item);
                return Task.CompletedTask;
            }

            public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                return Task.CompletedTask;
            }

            public int FunctionCount
            {
                get { return _state.Count; }
            }

            public void AssertFunctionCount(int expected, string failureMessage = null)
            {
                // Verify the event ordering and that we got all notifications. 
                foreach (var kv in _state)
                {
                    Assert.Equal("[start][postbind][complete]", kv.Value.ToString());
                }

                var actual = _state.Count;
                string msg = failureMessage ?? "Actual function invocations:" + Environment.NewLine + string.Join(Environment.NewLine, LogEntries.Select(l => l.FunctionName));
                Assert.True(actual == expected, $"Expected '{expected}'. Actual '{actual}'.{Environment.NewLine}{msg}");
            }
        }
    }
}
