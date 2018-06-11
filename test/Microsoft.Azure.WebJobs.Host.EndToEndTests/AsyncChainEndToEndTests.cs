// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
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

        private static CloudStorageAccount _storageAccount;

        private static RandomNameResolver _resolver;
        private static JobHostConfiguration _hostConfig;
        private readonly TestExceptionHandler _defaultExceptionHandler;
        private static EventWaitHandle _functionCompletedEvent;

        private static string _finalBlobContent;
        private static TimeSpan _timeoutJobDelay;

        private readonly CloudQueue _testQueue;
        private readonly TestFixture _fixture;

        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        public AsyncChainEndToEndTests(TestFixture fixture)
        {
            _fixture = fixture;
            _resolver = new RandomNameResolver();
            _hostConfig = new JobHostConfiguration()
            {
                NameResolver = _resolver,
                TypeLocator = new FakeTypeLocator(typeof(AsyncChainEndToEndTests))
            };

            _defaultExceptionHandler = new TestExceptionHandler();
            _hostConfig.AddService<IWebJobsExceptionHandler>(_defaultExceptionHandler);
            _hostConfig.Queues.MaxPollingInterval = TimeSpan.FromSeconds(2);

            _storageAccount = fixture.StorageAccount;
            _timeoutJobDelay = TimeSpan.FromMinutes(5);

            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_loggerProvider);
            _hostConfig.LoggerFactory = loggerFactory;
            _hostConfig.Aggregator.IsEnabled = false; // makes validation easier

            CloudQueueClient queueClient = _storageAccount.CreateCloudQueueClient();
            string queueName = _resolver.ResolveInString(TestQueueName);
            _testQueue = queueClient.GetQueueReference(queueName);
            if (!_testQueue.CreateIfNotExistsAsync().Result)
            {
                _testQueue.ClearAsync().Wait();
            }
        }

        [Fact]
        public async Task AsyncChainEndToEnd()
        {
            using (_functionCompletedEvent = new ManualResetEvent(initialState: false))
            {
                await AsyncChainEndToEndInternal();

                string firstQueueName = _resolver.ResolveInString(Queue1Name);
                string secondQueueName = _resolver.ResolveInString(Queue2Name);
                string blobContainerName = _resolver.ResolveInString(ContainerName);

                string[] loggerOutputLines = _loggerProvider.GetAllLogMessages()
                    .Where(p => p.FormattedMessage != null)
                    .SelectMany(p => p.FormattedMessage.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
                    .OrderBy(p => p)
                    .ToArray();

                string[] expectedOutputLines = new string[]
                {
                    "Found the following functions:",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.WriteStartDataMessageToQueue",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.QueueToQueueAsync",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.QueueToBlobAsync",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.AlwaysFailJob",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.DisabledJob",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.TimeoutJob",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.TimeoutJob_Throw",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.TimeoutJob_Throw_NoToken",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.BlobToBlobAsync",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.ReadResultBlob",
                    "Microsoft.Azure.WebJobs.Host.EndToEndTests.AsyncChainEndToEndTests.SystemParameterBindingOutput",
                    "Function 'AsyncChainEndToEndTests.DisabledJob' is disabled",
                    "Job host started",
                    "Executing 'AsyncChainEndToEndTests.WriteStartDataMessageToQueue' (Reason='This function was programmatically called via the host APIs.', Id=",
                    "Executed 'AsyncChainEndToEndTests.WriteStartDataMessageToQueue' (Succeeded, Id=",
                    string.Format("Executing 'AsyncChainEndToEndTests.QueueToQueueAsync' (Reason='New queue message detected on '{0}'.', Id=", firstQueueName),
                    "Executed 'AsyncChainEndToEndTests.QueueToQueueAsync' (Succeeded, Id=",
                    string.Format("Executing 'AsyncChainEndToEndTests.QueueToBlobAsync' (Reason='New queue message detected on '{0}'.', Id=", secondQueueName),
                    "Executed 'AsyncChainEndToEndTests.QueueToBlobAsync' (Succeeded, Id=",
                    string.Format("Executing 'AsyncChainEndToEndTests.BlobToBlobAsync' (Reason='New blob detected: {0}/Blob1', Id=", blobContainerName),
                    "Executed 'AsyncChainEndToEndTests.BlobToBlobAsync' (Succeeded, Id=",
                    "Job host stopped",
                    "Executing 'AsyncChainEndToEndTests.ReadResultBlob' (Reason='This function was programmatically called via the host APIs.', Id=",
                    "Executed 'AsyncChainEndToEndTests.ReadResultBlob' (Succeeded, Id=",
                    "User ILogger log",
                    "User TraceWriter log 1",
                    "User TraceWriter log 2"
                }.OrderBy(p => p).ToArray();

                bool hasError = loggerOutputLines.Any(p => p.Contains("Function had errors"));
                Assert.False(hasError);

                // Validate logger output
                for (int i = 0; i < expectedOutputLines.Length; i++)
                {
                    Assert.StartsWith(expectedOutputLines[i], loggerOutputLines[i]);
                }
            }
        }

        [Fact]
        public async Task AsyncChainEndToEnd_CustomFactories()
        {
            using (_functionCompletedEvent = new ManualResetEvent(initialState: false))
            {
                CustomQueueProcessorFactory queueProcessorFactory = new CustomQueueProcessorFactory();
                _hostConfig.Queues.QueueProcessorFactory = queueProcessorFactory;

                CustomStorageClientFactory storageClientFactory = new CustomStorageClientFactory();
                _hostConfig.StorageClientFactory = storageClientFactory;

                await AsyncChainEndToEndInternal();

                Assert.Equal(2, queueProcessorFactory.CustomQueueProcessors.Count);
                Assert.True(queueProcessorFactory.CustomQueueProcessors.All(p => p.Context.Queue.Name.StartsWith("asynce2eq")));
                Assert.True(queueProcessorFactory.CustomQueueProcessors.Sum(p => p.BeginProcessingCount) >= 2);
                Assert.True(queueProcessorFactory.CustomQueueProcessors.Sum(p => p.CompleteProcessingCount) >= 2);
            }
        }

        [Fact]
        public async Task LoggerLogging()
        {
            using (_functionCompletedEvent = new ManualResetEvent(initialState: false))
            {
                await AsyncChainEndToEndInternal();

                // Validate Logger
                bool hasError = _loggerProvider.GetAllLogMessages().Where(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Error")).Any();
                Assert.False(hasError);

                IEnumerable<string> userLogMessages = _loggerProvider.GetAllLogMessages()
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
                // enable the aggregator
                _hostConfig.Aggregator.IsEnabled = true;
                _hostConfig.Aggregator.BatchSize = 1;

                // add a FunctionEventCollector
                var eventCollector = new TestFunctionEventCollector();
                _hostConfig.AddService<IAsyncCollector<FunctionInstanceLogEntry>>(eventCollector);

                JobHost host = new JobHost(_hostConfig);

                await host.StartAsync();
                await host.CallAsync(typeof(AsyncChainEndToEndTests).GetMethod(nameof(WriteStartDataMessageToQueue)));

                await WaitForFunctionCompleteAsync();

                // ensure all logs have had a chance to flush
                await Task.Delay(3000);

                await host.StopAsync();

                // Make sure the aggregator was logged to
                var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Aggregator).Single();
                var count = logger.GetLogMessages().Count;
                Assert.True(count == 4, $"Expected 4. Actual {count}.{Environment.NewLine}{_loggerProvider.GetLogString()}");
                // Make sure the eventCollector was logged 
                eventCollector.AssertFunctionCount(4, _loggerProvider.GetLogString());
            }
        }

        [Fact]
        public async Task AggregatorOnly()
        {
            using (_functionCompletedEvent = new ManualResetEvent(initialState: false))
            {
                // enable the aggregator
                _hostConfig.Aggregator.IsEnabled = true;
                _hostConfig.Aggregator.BatchSize = 1;

                JobHost host = new JobHost(_hostConfig);

                await host.StartAsync();
                await host.CallAsync(typeof(AsyncChainEndToEndTests).GetMethod("WriteStartDataMessageToQueue"));

                await WaitForFunctionCompleteAsync();

                // ensure all logs have had a chance to flush
                await Task.Delay(3000);

                await host.StopAsync();

                // Make sure the aggregator was logged to
                var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Aggregator).Single();
                var count = logger.GetLogMessages().Count;

                Assert.True(count == 4, $"Expected 4. Actual {count}.{Environment.NewLine}{_loggerProvider.GetLogString()}");
            }
        }

        [Fact]
        public async Task EventCollectorOnly()
        {
            using (_functionCompletedEvent = new ManualResetEvent(initialState: false))
            {
                // disable the aggregator
                _hostConfig.Aggregator.IsEnabled = false;

                // add a FunctionEventCollector
                var eventCollector = new TestFunctionEventCollector();
                _hostConfig.AddService<IAsyncCollector<FunctionInstanceLogEntry>>(eventCollector);

                JobHost host = new JobHost(_hostConfig);

                await host.StartAsync();
                await host.CallAsync(typeof(AsyncChainEndToEndTests).GetMethod("WriteStartDataMessageToQueue"));

                await WaitForFunctionCompleteAsync();

                // ensure all logs have had a chance to flush
                await Task.Delay(3000);

                await host.StopAsync();

                // Make sure the aggregator was logged to
                var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Aggregator).SingleOrDefault();
                Assert.Null(logger);

                // Make sure the eventCollector was logged
                eventCollector.AssertFunctionCount(4, _loggerProvider.GetLogString());
            }
        }

        [Fact]
        public void FunctionFailures_LogsExpectedMessage()
        {
            JobHost host = new JobHost(_hostConfig);

            MethodInfo methodInfo = GetType().GetMethod("AlwaysFailJob");
            try
            {
                host.Call(methodInfo);
            }
            catch { }

            string expectedName = $"{methodInfo.DeclaringType.FullName}.{methodInfo.Name}";

            // Validate Logger
            // Logger only writes out a single log message (which includes the Exception).        
            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Results).Single();
            var logMessage = logger.GetLogMessages().Single();
            var loggerException = logMessage.Exception as FunctionException;
            Assert.NotNull(loggerException);
            Assert.Equal(expectedName, loggerException.MethodName);
        }

        [Fact]
        public async Task SystemParameterBindingOutput_GeneratesExpectedBlobs()
        {
            JobHost host = new JobHost(_hostConfig);

            var blobClient = _fixture.StorageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference("test-output");
            if (await container.ExistsAsync())
            {
                foreach (CloudBlockBlob blob in (await container.ListBlobsSegmentedAsync(null)).Results)
                {
                    await blob.DeleteAsync();
                }
            }

            MethodInfo methodInfo = GetType().GetMethod("SystemParameterBindingOutput");
            var arguments = new Dictionary<string, object>
            {
                { "input", "Test Value" }
            };
            host.Call(methodInfo, arguments);

            // We expect 3 separate blobs to have been written
            var blobs = (await container.ListBlobsSegmentedAsync(null)).Results.Cast<CloudBlockBlob>().ToArray();
            Assert.Equal(3, blobs.Length);
            foreach (var blob in blobs)
            {
                string content = await blob.DownloadTextAsync();
                Assert.Equal("Test Value", content.Trim(new char[] { '\uFEFF', '\u200B' }));
            }
        }

        [Fact]
        public async Task Timeout_TimeoutExpires_Cancels()
        {
            var exceptionHandler = new TestExceptionHandler();
            await RunTimeoutTest(exceptionHandler, typeof(TaskCanceledException), nameof(TimeoutJob));
            Assert.Empty(exceptionHandler.UnhandledExceptionInfos);
            Assert.Empty(exceptionHandler.TimeoutExceptionInfos);
        }

        [Fact]
        public async Task TimeoutWithThrow_TimeoutExpires_CancelsAndThrows()
        {
            var exceptionHandler = new TestExceptionHandler();
            await RunTimeoutTest(exceptionHandler, typeof(FunctionTimeoutException), nameof(TimeoutJob_Throw));
            var exception = exceptionHandler.TimeoutExceptionInfos.Single().SourceException;
            Assert.IsType<FunctionTimeoutException>(exception);
            Assert.Empty(exceptionHandler.UnhandledExceptionInfos);
        }

        [Fact]
        public async Task TimeoutWithThrow_NoCancellationToken_CancelsAndThrows()
        {
            var exceptionHandler = new TestExceptionHandler();
            await RunTimeoutTest(exceptionHandler, typeof(FunctionTimeoutException), nameof(TimeoutJob_Throw_NoToken));
            var exception = exceptionHandler.TimeoutExceptionInfos.Single().SourceException;
            Assert.IsType<FunctionTimeoutException>(exception);
            Assert.Empty(exceptionHandler.UnhandledExceptionInfos);
        }

        private async Task RunTimeoutTest(IWebJobsExceptionHandler exceptionHandler, Type expectedExceptionType, string functionName)
        {
            try
            {
                _hostConfig.AddService<IWebJobsExceptionHandler>(exceptionHandler);
                JobHost host = new JobHost(_hostConfig);

                try
                {
                    await host.StartAsync();

                    MethodInfo methodInfo = GetType().GetMethod(functionName);

                    Exception ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
                    {
                        await host.CallAsync(methodInfo);
                    });

                    Assert.IsType(expectedExceptionType, ex);
                }
                finally
                {
                    host.Stop();
                }

                string expectedExceptionMessage = $"Timeout value of 00:00:01 exceeded by function 'AsyncChainEndToEndTests.{functionName}'";
                string expectedResultMessage = $"Executed 'AsyncChainEndToEndTests.{functionName}' (Failed, Id=";

                // Validate Logger
                var resultLogger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Results).Single();
                var executorLogger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.CreateFunctionCategory(functionName)).Single();

                // Results logs the exception with no message
                Assert.NotNull(resultLogger.GetLogMessages().Single().Exception);
                Assert.Null(resultLogger.GetLogMessages().Single().FormattedMessage);

                // It logs Executed/Executing messages and the timeout message.
                Assert.Equal(3, executorLogger.GetLogMessages().Count());

                executorLogger.GetLogMessages().Single(p => p.FormattedMessage != null && p.FormattedMessage.StartsWith(expectedExceptionMessage));
                LogMessage resultMessage = executorLogger.GetLogMessages().Single(p => p.FormattedMessage != null && p.FormattedMessage.StartsWith(expectedResultMessage));
                Assert.NotNull(resultMessage.Exception);
            }
            finally
            {
                _hostConfig.AddService<IWebJobsExceptionHandler>(_defaultExceptionHandler);
            }
        }

        [Fact]
        public async Task Timeout_NoExpiry_CompletesSuccessfully()
        {
            JobHost host = new JobHost(_hostConfig);

            _timeoutJobDelay = TimeSpan.FromSeconds(0);
            MethodInfo methodInfo = GetType().GetMethod("TimeoutJob");
            await host.CallAsync(methodInfo);

            // Validate Logger
            LogMessage[] logErrors = _loggerProvider.GetAllLogMessages().Where(l => l.Level == Microsoft.Extensions.Logging.LogLevel.Error).ToArray();
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

        public static async Task QueueToQueueAsync(
            [QueueTrigger(Queue1Name)] string message,
            [Queue(Queue2Name)] IAsyncCollector<string> output,
            CancellationToken token,
            TraceWriter trace)
        {
            CloudBlobClient blobClient = _storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(_resolver.ResolveInString(ContainerName));
            CloudBlockBlob blob = container.GetBlockBlobReference(NonWebJobsBlobName);

            string blobContent = await blob.DownloadTextAsync();

            trace.Info("User TraceWriter log 1");

            await output.AddAsync(blobContent + message);
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
            Guid g;
            Assert.True(Guid.TryParse(parentId, out g));
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

        private async Task AsyncChainEndToEndInternal()
        {
            JobHost host = new JobHost(_hostConfig);

            Assert.Null(_hostConfig.HostId);

            await host.StartAsync();

            Assert.NotEmpty(_hostConfig.HostId);

            await host.CallAsync(typeof(AsyncChainEndToEndTests).GetMethod("WriteStartDataMessageToQueue"));

            await WaitForFunctionCompleteAsync();

            // ensure all logs have had a chance to flush
            await Task.Delay(3000);

            // Stop async waits for the function to complete
            await host.StopAsync();

            await host.CallAsync(typeof(AsyncChainEndToEndTests).GetMethod("ReadResultBlob"));
            Assert.Equal("async works", _finalBlobContent);
        }

        private async Task WaitForFunctionCompleteAsync(int waitTimeout = 30000)
        {
            await TestHelpers.Await(() => _functionCompletedEvent.WaitOne(200), timeout: waitTimeout, pollingInterval: 500,
                userMessageCallback: () => $"Function did not complete in {waitTimeout} ms. Current time: {DateTime.UtcNow.ToString("HH:mm:ss.fff")}{Environment.NewLine}{_loggerProvider.GetLogString()}");
        }

        private class CustomQueueProcessorFactory : IQueueProcessorFactory
        {
            public List<CustomQueueProcessor> CustomQueueProcessors = new List<CustomQueueProcessor>();

            public QueueProcessor Create(QueueProcessorFactoryContext context)
            {
                // demonstrates how the Queue.ServiceClient options can be configured
                context.Queue.ServiceClient.DefaultRequestOptions.ServerTimeout = TimeSpan.FromSeconds(30);

                // demonstrates how queue options can be customized
                context.Queue.EncodeMessage = true;

                // demonstrates how batch processing behavior and other knobs
                // can be customized
                context.BatchSize = 30;
                context.NewBatchThreshold = 100;
                context.MaxPollingInterval = TimeSpan.FromSeconds(15);

                CustomQueueProcessor processor = new CustomQueueProcessor(context);
                CustomQueueProcessors.Add(processor);
                return processor;
            }
        }

        public class CustomQueueProcessor : QueueProcessor
        {
            public int BeginProcessingCount = 0;
            public int CompleteProcessingCount = 0;

            public CustomQueueProcessor(QueueProcessorFactoryContext context) : base(context)
            {
                Context = context;
            }

            public QueueProcessorFactoryContext Context { get; private set; }

            public override Task<bool> BeginProcessingMessageAsync(CloudQueueMessage message, CancellationToken cancellationToken)
            {
                BeginProcessingCount++;
                return base.BeginProcessingMessageAsync(message, cancellationToken);
            }

            public override Task CompleteProcessingMessageAsync(CloudQueueMessage message, FunctionResult result, CancellationToken cancellationToken)
            {
                CompleteProcessingCount++;
                return base.CompleteProcessingMessageAsync(message, result, cancellationToken);
            }

            protected override async Task ReleaseMessageAsync(CloudQueueMessage message, FunctionResult result, TimeSpan visibilityTimeout, CancellationToken cancellationToken)
            {
                // demonstrates how visibility timeout for failed messages can be customized
                // the logic here could implement exponential backoff, etc.
                visibilityTimeout = TimeSpan.FromSeconds(message.DequeueCount);

                await base.ReleaseMessageAsync(message, result, visibilityTimeout, cancellationToken);
            }
        }

        /// <summary>
        /// This custom <see cref="StorageClientFactory"/> demonstrates how clients can be customized.
        /// For example, users can configure global retry policies, DefaultRequestOptions, etc.
        /// </summary>
        public class CustomStorageClientFactory : StorageClientFactory
        {
            public int TotalBlobClientCount;
            public int TotalQueueClientCount;
            public int TotalTableClientCount;

            public int ParameterBlobClientCount;
            public int ParameterQueueClientCount;
            public int ParameterTableClientCount;

            public override CloudBlobClient CreateCloudBlobClient(StorageClientFactoryContext context)
            {
                TotalBlobClientCount++;

                if (context.Parameter != null)
                {
                    ParameterBlobClientCount++;
                }

                return base.CreateCloudBlobClient(context);
            }

            public override CloudQueueClient CreateCloudQueueClient(StorageClientFactoryContext context)
            {
                TotalQueueClientCount++;

                if (context.Parameter != null)
                {
                    ParameterQueueClientCount++;

                    if (context.Parameter.Member.Name == "QueueToQueueAsync")
                    {
                        // demonstrates how context can be used to create a custom client
                        // for a particular method or parameter binding
                    }
                }

                return base.CreateCloudQueueClient(context);
            }

            public override CloudTableClient CreateCloudTableClient(StorageClientFactoryContext context)
            {
                TotalTableClientCount++;

                if (context.Parameter != null)
                {
                    ParameterTableClientCount++;
                }

                return base.CreateCloudTableClient(context);
            }
        }

        public class TestFixture : IDisposable
        {
            public TestFixture()
            {
                JobHostConfiguration config = new JobHostConfiguration();
                StorageAccount = CloudStorageAccount.Parse(config.StorageConnectionString);
            }

            public CloudStorageAccount StorageAccount
            {
                get;
                private set;
            }

            public void Dispose()
            {
                CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
                foreach (var testContainer in blobClient.ListContainersSegmentedAsync(TestArtifactsPrefix, null).Result.Results)
                {
                    testContainer.DeleteAsync().Wait();
                }

                CloudQueueClient queueClient = StorageAccount.CreateCloudQueueClient();
                foreach (var testQueue in queueClient.ListQueuesSegmentedAsync(TestArtifactsPrefix, null).Result.Results)
                {
                    testQueue.DeleteAsync().Wait();
                }
            }
        }

        private class TestExceptionHandler : IWebJobsExceptionHandler
        {
            public ICollection<ExceptionDispatchInfo> UnhandledExceptionInfos { get; private set; }
            public ICollection<ExceptionDispatchInfo> TimeoutExceptionInfos { get; private set; }

            public void Initialize(JobHost host)
            {
                UnhandledExceptionInfos = new List<ExceptionDispatchInfo>();
                TimeoutExceptionInfos = new List<ExceptionDispatchInfo>();
            }

            public Task OnTimeoutExceptionAsync(ExceptionDispatchInfo exceptionInfo, TimeSpan timeoutGracePeriod)
            {
                TimeoutExceptionInfos.Add(exceptionInfo);
                return Task.FromResult(0);
            }

            public Task OnUnhandledExceptionAsync(ExceptionDispatchInfo exceptionInfo)
            {
                // TODO: FACAVAL - Validate this, tests are stepping over each other.
                if (!(exceptionInfo.SourceException is StorageException storageException &&
                    storageException?.InnerException is TaskCanceledException))
                {
                    UnhandledExceptionInfos.Add(exceptionInfo);
                }
                return Task.FromResult(0);
            }
        }

        private class TestFunctionEventCollector : IAsyncCollector<FunctionInstanceLogEntry>
        {
            private List<FunctionInstanceLogEntry> LogEntries { get; } = new List<FunctionInstanceLogEntry>();

            public Dictionary<Guid, StringBuilder> _state = new Dictionary<Guid, StringBuilder>();

            public Task AddAsync(FunctionInstanceLogEntry item, CancellationToken cancellationToken = default(CancellationToken))
            {
                StringBuilder prevState;
                if (!_state.TryGetValue(item.FunctionInstanceId, out prevState))
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
                string msg = failureMessage ?? "Actual function invocations:" + Environment.NewLine + string.Join(Environment.NewLine, this.LogEntries.Select(l => l.FunctionName));
                Assert.True(actual == expected, $"Expected '{expected}'. Actual '{actual}'.{Environment.NewLine}{msg}");
            }
        }
    }
}
