// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class JobHostTests
    {
        // Checks that we write the marker file when we call the host
        [Fact]
        public void TestSdkMarkerIsWrittenWhenInAzureWebSites()
        {
            // Arrange
            string tempDir = Path.GetTempPath();
            const string filename = "WebJobsSdk.marker";

            var path = Path.Combine(tempDir, filename);

            File.Delete(path);

            var host = new HostBuilder().ConfigureDefaultTestHost().Build();

            using (host)
            {
                try
                {
                    Environment.SetEnvironmentVariable(WebSitesKnownKeyNames.JobDataPath, tempDir);

                    // Act
                    host.Start();

                    // Assert
                    Assert.True(File.Exists(path), "SDK marker file should have been written");
                }
                finally
                {
                    Environment.SetEnvironmentVariable(WebSitesKnownKeyNames.JobDataPath, null);
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public async Task StartAsync_WhenNotStarted_DoesNotThrow()
        {
            // Arrange
            var host = new HostBuilder().ConfigureDefaultTestHost().Build().GetJobHost();

            using (host)
            {
                // Act & Assert
                await host.StartAsync();
            }
        }

        [Fact]
        public async Task StartAsync_WhenStarted_Throws()
        {
            // Arrange
            var host = new HostBuilder().ConfigureDefaultTestHost().Build().GetJobHost();

            using (host)
            {
                var tIgnore = host.StartAsync();

                // Act & Assert
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());
                Assert.Equal("Start has already been called.", exception.Message);
            }
        }

        [Fact]
        public async Task StartAsync_WhenStopped_Throws()
        {
            // Arrange
            var host = new HostBuilder().ConfigureDefaultTestHost().Build().GetJobHost();

            using (host)
            {
                await host.StartAsync();
                await host.StopAsync();

                // Act & Assert
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());
                Assert.Equal("Start has already been called.", exception.Message);
            }
        }

        [Fact]
        public async Task StartAsync_WhenStarting_Throws()
        {
            // Arrange
            // Create a way to block StartAsync.
            TaskCompletionSource<JobHostContext> createTaskSource = new TaskCompletionSource<JobHostContext>();
            var provider = new LambdaJobHostContextFactory((a, b) => createTaskSource.Task);

            var host = new HostBuilder()
                .ConfigureDefaultTestHost()
                .ConfigureServices(s =>
                {
                    s.AddSingleton<IJobHostContextFactory>(_ => provider);
                })
                .Build()
                .GetJobHost();

            using (host)
            {
                Task starting = host.StartAsync();
                Assert.False(starting.IsCompleted); // Guard

                // Act & Assert
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());
                Assert.Equal("Start has already been called.", exception.Message);

                // Cleanup
                createTaskSource.SetResult(new JobHostContext(null, null, new Mock<IListener>().Object, null, null));
                await starting;
            }
        }

        [Fact]
        public async Task StartAsync_WhenStopping_Throws()
        {
            // Arrange
            var host = new HostBuilder().ConfigureDefaultTestHost().Build().GetJobHost();

            using (host)
            {
                await host.StartAsync();

                // Replace (and cleanup) the exsiting runner to hook StopAsync.
                IListener oldListener = host.Listener;
                oldListener.StopAsync(CancellationToken.None).GetAwaiter().GetResult();

                TaskCompletionSource<object> stopTaskSource = new TaskCompletionSource<object>();
                Mock<IListener> listenerMock = new Mock<IListener>(MockBehavior.Strict);
                listenerMock.Setup(r => r.StopAsync(It.IsAny<CancellationToken>())).Returns(stopTaskSource.Task);
                listenerMock.Setup(r => r.Dispose());
                host.Listener = listenerMock.Object;

                Task stopping = host.StopAsync();

                // Act & Assert
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());
                Assert.Equal("Start has already been called.", exception.Message);

                // Cleanup
                stopTaskSource.SetResult(null);
                stopping.GetAwaiter().GetResult();
            }
        }

        [Fact]
        public async Task StopAsync_WhenStarted_DoesNotThrow()
        {
            // Arrange
            var host = new HostBuilder().ConfigureDefaultTestHost().Build().GetJobHost();

            using (host)
            {
                await host.StartAsync();

                // Act & Assert
                host.StopAsync().GetAwaiter().GetResult();
            }
        }

        [Fact]
        public async Task StopAsync_WhenStopped_DoesNotThrow()
        {
            // Arrange
            var host = new HostBuilder().ConfigureDefaultTestHost().Build().GetJobHost();

            using (host)
            {
                await host.StartAsync();
                await host.StopAsync();

                // Act & Assert
                await host.StopAsync();
            }
        }

        [Fact]
        public async Task StopAsync_WhenNotStarted_Throws()
        {
            // Arrange
            var host = new HostBuilder().ConfigureDefaultTestHost().Build().GetJobHost();

            using (host)
            {
                // Act & Assert
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StopAsync());
                Assert.Equal("The host has not yet started.", exception.Message);
            }
        }

        [Fact]
        public async Task StopAsync_WhenStarting_Throws()
        {
            // Arrange
            // Create a way to block StartAsync.
            TaskCompletionSource<JobHostContext> createTaskSource = new TaskCompletionSource<JobHostContext>();
            var provider = new LambdaJobHostContextFactory((a, b) => createTaskSource.Task);

            var host = new HostBuilder()
                .ConfigureDefaultTestHost()
                .ConfigureServices(s =>
                {
                    s.AddSingleton<IJobHostContextFactory>(_ => provider);
                })
                .Build()
                .GetJobHost();

            using (host)
            {
                Task starting = host.StartAsync();
                Assert.False(starting.IsCompleted); // Guard

                // Act & Assert
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StopAsync());
                Assert.Equal("The host has not yet started.", exception.Message);

                // Cleanup
                createTaskSource.SetResult(new JobHostContext(null, null, new Mock<IListener>().Object, null, null));
                starting.GetAwaiter().GetResult();
            }
        }

        [Fact]
        public async Task StopAsync_WhenWaiting_ReturnsIncompleteTask()
        {
            // Arrange
            var host = new HostBuilder().ConfigureDefaultTestHost().Build().GetJobHost();

            using (host)
            {
                await host.StartAsync();

                // Replace (and cleanup) the existing listener to hook StopAsync.
                IListener oldListener = host.Listener;
                oldListener.StopAsync(CancellationToken.None).GetAwaiter().GetResult();

                TaskCompletionSource<object> stopTaskSource = new TaskCompletionSource<object>();
                Mock<IListener> listenerMock = new Mock<IListener>(MockBehavior.Strict);
                listenerMock.Setup(r => r.StopAsync(It.IsAny<CancellationToken>())).Returns(stopTaskSource.Task);
                listenerMock.Setup(r => r.Dispose());
                host.Listener = listenerMock.Object;

                // Act
                Task stopping = host.StopAsync();

                // Assert
                Assert.False(stopping.IsCompleted);

                // Cleanup
                stopTaskSource.SetResult(null);
                stopping.GetAwaiter().GetResult();
            }
        }

        [Fact]
        public async Task StopAsync_WhenAlreadyStopping_ReturnsSameTask()
        {
            // Arrange
            var host = new HostBuilder().ConfigureDefaultTestHost().Build().GetJobHost();

            using (host)
            {
                await host.StartAsync();

                // Replace (and cleanup) the existing listener to hook StopAsync.
                IListener oldRunner = host.Listener;
                oldRunner.StopAsync(CancellationToken.None).GetAwaiter().GetResult();

                TaskCompletionSource<object> stopTaskSource = new TaskCompletionSource<object>();
                Mock<IListener> listenerMock = new Mock<IListener>(MockBehavior.Strict);
                listenerMock.Setup(r => r.StopAsync(It.IsAny<CancellationToken>())).Returns(stopTaskSource.Task);
                listenerMock.Setup(r => r.Dispose());
                host.Listener = listenerMock.Object;
                Task alreadyStopping = host.StopAsync();

                // Act
                Task stoppingAgain = host.StopAsync();

                // Assert
                Assert.Same(alreadyStopping, stoppingAgain);

                // Cleanup
                stopTaskSource.SetResult(null);
                alreadyStopping.GetAwaiter().GetResult();
                stoppingAgain.GetAwaiter().GetResult();
            }
        }

        [Fact]
        public async Task CallAsync_WithDictionary()
        {
            var host = JobHostFactory.Create<ProgramSimple>(null);

            var value = "abc";
            ProgramSimple._value = null;
            await host.CallAsync("Test", new Dictionary<string, object> { { "value", value } });

            // Ensure test method was invoked properly.
            Assert.Equal(value, ProgramSimple._value);
        }

        [Fact]
        public async Task CallAsync_WithObject()
        {
            var host = JobHostFactory.Create<ProgramSimple>(null);

            var x = "abc";
            ProgramSimple._value = null;
            await host.CallAsync("Test", new { value = x });

            // Ensure test method was invoked properly.
            Assert.Equal(x, ProgramSimple._value);
        }

        [Fact]
        public void CallAsync_WithCancellationToken_PassesCancellationTokenToMethod()
        {
            // Arrange
            ProgramWithCancellationToken.Cleanup();
            var host = JobHostFactory.Create<ProgramWithCancellationToken>(null);

            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                ProgramWithCancellationToken.CancellationTokenSource = source;

                // Act
                host.CallAsync("BindCancellationToken", null, source.Token).GetAwaiter().GetResult();

                // Assert
                Assert.True(ProgramWithCancellationToken.IsCancellationRequested);
            }
        }

        [Fact]
        public async Task CallAsync_WhenMethodThrows_PreservesStackTrace()
        {
            try
            {
                // Arrange
                InvalidOperationException expectedException = new InvalidOperationException();
                ExceptionDispatchInfo expectedExceptionInfo = CreateExceptionInfo(expectedException);
                string expectedStackTrace = expectedExceptionInfo.SourceException.StackTrace;
                ThrowingProgram.ExceptionInfo = expectedExceptionInfo;

                var host = JobHostFactory.Create<ThrowingProgram>(null);
                MethodInfo methodInfo = typeof(ThrowingProgram).GetMethod("Throw");

                // Act & Assert
                FunctionInvocationException exception = await Assert.ThrowsAsync<FunctionInvocationException>(
                    () => host.CallAsync(methodInfo));
                Assert.Same(exception.InnerException, expectedException);
                Assert.NotNull(exception.InnerException.StackTrace);
                Assert.True(exception.InnerException.StackTrace.StartsWith(expectedStackTrace));
            }
            finally
            {
                ThrowingProgram.ExceptionInfo = null;
            }
        }

        [Fact]
        public async Task BlobTrigger_ProvidesBlobTriggerBindingData()
        {
            try
            {
                // Arrange
                var host = new HostBuilder()
                    .ConfigureDefaultTestHost<BlobTriggerBindingDataProgram>(c =>
                    {
                        c.AddAzureStorage();
                    })
                    .Build()
                    .GetJobHost();

                using (host)
                {
                    CloudStorageAccount account = CloudStorageAccount.DevelopmentStorageAccount;

                    MethodInfo methodInfo = typeof(BlobTriggerBindingDataProgram).GetMethod(nameof(BlobTriggerBindingDataProgram.OnBlob));
                    string containerName = "a";
                    string blobName = "b";
                    string expectedPath = containerName + "/" + blobName;
                    CloudBlobContainer container = account.CreateCloudBlobClient().GetContainerReference(containerName);
                    ICloudBlob blob = container.GetBlockBlobReference(blobName);

                    // Act
                    await host.CallAsync(methodInfo, new { blob = blob });

                    // Assert
                    Assert.Equal(expectedPath, BlobTriggerBindingDataProgram.BlobTrigger);
                }
            }
            finally
            {
                BlobTriggerBindingDataProgram.BlobTrigger = null;
            }
        }

        [Fact]
        public async Task QueueTrigger_ProvidesQueueTriggerBindingData()
        {
            try
            {
                // Arrange
                var host = new HostBuilder()
                    .ConfigureDefaultTestHost<QueueTriggerBindingDataProgram>(c =>
                    {
                        c.AddAzureStorage();
                    })
                    .Build()
                    .GetJobHost();

                using (host)
                {
                    MethodInfo methodInfo = typeof(QueueTriggerBindingDataProgram).GetMethod(nameof(QueueTriggerBindingDataProgram.OnQueue));
                    string expectedMessage = "a";

                    // Act
                    await host.CallAsync(methodInfo, new { message = expectedMessage });

                    // Assert
                    Assert.Equal(expectedMessage, QueueTriggerBindingDataProgram.QueueTrigger);
                }
            }
            finally
            {
                QueueTriggerBindingDataProgram.QueueTrigger = null;
            }
        }

        [Fact]
        public async Task QueueTrigger_WithTextualByteArrayMessage_ProvidesQueueTriggerBindingData()
        {
            try
            {
                // Arrange
                var host = new HostBuilder()
                    .ConfigureDefaultTestHost<QueueTriggerBindingDataProgram>(c =>
                    {
                        c.AddAzureStorage();
                    })
                    .Build()
                    .GetJobHost();

                using (host)
                {
                    MethodInfo methodInfo = typeof(QueueTriggerBindingDataProgram).GetMethod(nameof(QueueTriggerBindingDataProgram.OnQueue));
                    string expectedMessage = "abc";
                    CloudQueueMessage message = new CloudQueueMessage(expectedMessage);
                    Assert.Equal(expectedMessage, message.AsString); // Guard

                    // Act
                    await host.CallAsync(methodInfo, new { message });

                    // Assert
                    Assert.Equal(expectedMessage, QueueTriggerBindingDataProgram.QueueTrigger);
                }
            }
            finally
            {
                QueueTriggerBindingDataProgram.QueueTrigger = null;
            }
        }

        [Fact]
        public async Task QueueTrigger_WithNonTextualByteArrayMessageUsingQueueTriggerBindingData_Throws()
        {
            try
            {
                // Arrange
                var host = new HostBuilder()
                    .ConfigureDefaultTestHost<QueueTriggerBindingDataProgram>(c =>
                    {
                        c.AddAzureStorage();
                    })
                    .Build()
                    .GetJobHost();

                using (host)
                {
                    MethodInfo methodInfo = typeof(QueueTriggerBindingDataProgram).GetMethod(nameof(QueueTriggerBindingDataProgram.OnQueue));
                    byte[] contents = new byte[] { 0x00, 0xFF }; // Not valid UTF-8
                    CloudQueueMessage message = CloudQueueMessage.CreateCloudQueueMessageFromByteArray(contents);

                    // Act & Assert
                    FunctionInvocationException exception = await Assert.ThrowsAsync<FunctionInvocationException>(
                        () => host.CallAsync(methodInfo, new { message = message }));
                    // This exeption shape/message could be better, but it's meets a minimum acceptibility threshold.
                    Assert.Equal("Exception binding parameter 'queueTrigger'", exception.InnerException.Message);
                    Exception innerException = exception.InnerException.InnerException;
                    Assert.IsType<InvalidOperationException>(innerException);
                    Assert.Equal("Binding data does not contain expected value 'queueTrigger'.", innerException.Message);
                }
            }
            finally
            {
                QueueTriggerBindingDataProgram.QueueTrigger = null;
            }
        }

        [Fact]
        public async Task QueueTrigger_WithNonTextualByteArrayMessageNotUsingQueueTriggerBindingData_DoesNotThrow()
        {
            try
            {
                // Arrange
                var host = new HostBuilder()
                    .ConfigureDefaultTestHost<QueueTriggerBindingDataProgram>(c =>
                    {
                        c.AddAzureStorage();
                    })
                    .Build()
                    .GetJobHost();

                using (host)
                {
                    MethodInfo methodInfo = typeof(QueueTriggerBindingDataProgram).GetMethod(nameof(QueueTriggerBindingDataProgram.ProcessQueueAsBytes));
                    byte[] expectedBytes = new byte[] { 0x00, 0xFF }; // Not valid UTF-8
                    CloudQueueMessage message = CloudQueueMessage.CreateCloudQueueMessageFromByteArray(expectedBytes);

                    // Act
                    await host.CallAsync(methodInfo, new { message = message });

                    // Assert
                    Assert.Equal(QueueTriggerBindingDataProgram.Bytes, expectedBytes);
                }
            }
            finally
            {
                QueueTriggerBindingDataProgram.QueueTrigger = null;
            }
        }

        [Fact]
        [Trait("Category", "secretsrequired")]
        public async Task IndexingExceptions_CanBeHandledByLogger()
        {
            FunctionErrorLogger errorLogger = new FunctionErrorLogger("TestCategory");

            Mock<ILoggerProvider> mockProvider = new Mock<ILoggerProvider>(MockBehavior.Strict);
            mockProvider
                .Setup(m => m.CreateLogger(It.IsAny<string>()))
                .Returns(errorLogger);

            var builder = new HostBuilder()
                .ConfigureDefaultTestHost<BindingErrorsProgram>(b =>
                {
                    b.AddAzureStorage();
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddProvider(mockProvider.Object);
                });

            var host = builder.Build();
            using (host)
            {
                await host.StartAsync();

                // verify the handled binding error
                FunctionIndexingException fex = errorLogger.Errors.SingleOrDefault() as FunctionIndexingException;
                Assert.True(fex.Handled);
                Assert.Equal("BindingErrorsProgram.Invalid", fex.MethodName);

                // verify that the binding error was logged
                Assert.Equal(5, errorLogger.GetLogMessages().Count);

                // Skip validating the initial 'Starting JobHost' message.

                LogMessage logMessage = errorLogger.GetLogMessages()[1];
                Assert.Equal("Error indexing method 'BindingErrorsProgram.Invalid'", logMessage.FormattedMessage);
                Assert.Same(fex, logMessage.Exception);
                Assert.Equal("Invalid container name: invalid$=+1", logMessage.Exception.InnerException.Message);

                logMessage = errorLogger.GetLogMessages()[2];
                Assert.Equal("Function 'BindingErrorsProgram.Invalid' failed indexing and will be disabled.", logMessage.FormattedMessage);

                // verify that the valid function was still indexed
                logMessage = errorLogger.GetLogMessages()[3];
                Assert.True(logMessage.FormattedMessage.Contains("Found the following functions"));
                Assert.True(logMessage.FormattedMessage.Contains("BindingErrorsProgram.Valid"));

                // verify that the job host was started successfully
                logMessage = errorLogger.GetLogMessages()[4];
                Assert.Equal("Job host started", logMessage.FormattedMessage);

                await host.StopAsync();
            }
        }

        private static ExceptionDispatchInfo CreateExceptionInfo(Exception exception)
        {
            try
            {
                throw exception;
            }
            catch (Exception caught)
            {
                return ExceptionDispatchInfo.Capture(caught);
            }
        }

        private class ProgramSimple
        {
            public static string _value; // evidence of execution

            [NoAutomaticTrigger]
            public static void Test(string value)
            {
                _value = value;
            }
        }

        private class LambdaJobHostContextFactory : IJobHostContextFactory
        {
            private readonly Func<CancellationToken, CancellationToken, Task<JobHostContext>> _create;

            public LambdaJobHostContextFactory(Func<CancellationToken, CancellationToken, Task<JobHostContext>> create)
            {
                _create = create;
            }

            public Task<JobHostContext> Create(CancellationToken shutdownToken, CancellationToken cancellationToken)
            {
                return _create.Invoke(shutdownToken, cancellationToken);
            }
        }

        private class ProgramWithCancellationToken
        {
            public static CancellationTokenSource CancellationTokenSource { get; set; }

            public static bool IsCancellationRequested { get; private set; }

            public static void Cleanup()
            {
                CancellationTokenSource = null;
                IsCancellationRequested = false;
            }

            [NoAutomaticTrigger]
            public static void BindCancellationToken(CancellationToken cancellationToken)
            {
                CancellationTokenSource.Cancel();
                IsCancellationRequested = cancellationToken.IsCancellationRequested;
            }
        }

        private class ThrowingProgram
        {
            public static ExceptionDispatchInfo ExceptionInfo { get; set; }

            [NoAutomaticTrigger]
            public static void Throw()
            {
                ExceptionInfo.Throw();
            }
        }

        private class BlobTriggerBindingDataProgram
        {
            public static string BlobTrigger { get; set; }

            public static void OnBlob([BlobTrigger("ignore/{name}")] ICloudBlob blob, string blobTrigger)
            {
                BlobTrigger = blobTrigger;
            }
        }

        private class QueueTriggerBindingDataProgram
        {
            public static string QueueTrigger { get; set; }
            public static byte[] Bytes { get; set; }

            public static void OnQueue([QueueTrigger("ignore")] CloudQueueMessage message, string queueTrigger)
            {
                QueueTrigger = queueTrigger;
            }

            public static void ProcessQueueAsBytes([QueueTrigger("ignore")] byte[] message)
            {
                Bytes = message;
            }
        }

        private class BindingErrorsProgram
        {
            // Invalid function
            public static void Invalid([BlobTrigger("invalid$=+1")] string blob)
            {
            }

            // Valid function
            public static void Valid([BlobTrigger("test")] string blob)
            {
            }
        }

        private class FunctionErrorLogger : TestLogger
        {
            public Collection<Exception> Errors = new Collection<Exception>();

            public FunctionErrorLogger(string category) :
                base(category, null)
            {
            }

            public override void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                FunctionIndexingException fex = exception as FunctionIndexingException;
                if (fex != null)
                {
                    fex.Handled = true;
                    Errors.Add(fex);
                }

                base.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}
