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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    // TODO: Are these meant to be tests?
    public class JobHostTests
    {
        // Checks that we write the marker file when we call the host
        public void TestSdkMarkerIsWrittenWhenInAzureWebSites()
        {
            // Arrange
            string tempDir = Path.GetTempPath();
            const string filename = "WebJobsSdk.marker";

            var path = Path.Combine(tempDir, filename);

            File.Delete(path);

            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(new JobHostOptions()), new Mock<IJobHostContextFactory>().Object))
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

        public void StartAsync_WhenNotStarted_DoesNotThrow()
        {
            // Arrange
            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(new JobHostOptions()), new Mock<IJobHostContextFactory>().Object))
            {
                // Act & Assert
                host.StartAsync().GetAwaiter().GetResult();
            }
        }

        public void StartAsync_WhenStarted_Throws()
        {
            // Arrange
            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(new JobHostOptions()), new Mock<IJobHostContextFactory>().Object))
            {
                host.Start();

                // Act & Assert
                ExceptionAssert.ThrowsInvalidOperation(() => host.StartAsync(), "Start has already been called.");
            }
        }

        public void StartAsync_WhenStopped_Throws()
        {
            // Arrange
            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(new JobHostOptions()), new Mock<IJobHostContextFactory>().Object))
            {
                host.Start();
                host.Stop();

                // Act & Assert
                ExceptionAssert.ThrowsInvalidOperation(() => host.StartAsync(), "Start has already been called.");
            }
        }

        
        public void StartAsync_WhenStarting_Throws()
        {
            // Arrange
            // TaskCompletionSource<IStorageAccount> getAccountTaskSource = new TaskCompletionSource<IStorageAccount>();
            //JobHostOptions configuration = CreateConfiguration(new LambdaStorageAccountProvider(
            //        (i1, i2) => getAccountTaskSource.Task));

            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(new JobHostOptions()), new Mock<IJobHostContextFactory>().Object))
            {
                Task starting = host.StartAsync();
                Assert.False(starting.IsCompleted); // Guard

                // Act & Assert
                ExceptionAssert.ThrowsInvalidOperation(() => host.StartAsync(), "Start has already been called.");

                // Cleanup
                // getAccountTaskSource.SetResult(null);
                starting.GetAwaiter().GetResult();
            }
        }
        

        public void StartAsync_WhenStopping_Throws()
        {
            // Arrange
            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(new JobHostOptions()), new Mock<IJobHostContextFactory>().Object))
            {
                host.Start();

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
                ExceptionAssert.ThrowsInvalidOperation(() => host.StartAsync(), "Start has already been called.");

                // Cleanup
                stopTaskSource.SetResult(null);
                stopping.GetAwaiter().GetResult();
            }
        }

        public void StopAsync_WhenStarted_DoesNotThrow()
        {
            // Arrange
            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(new JobHostOptions()), new Mock<IJobHostContextFactory>().Object))
            {
                host.Start();

                // Act & Assert
                host.StopAsync().GetAwaiter().GetResult();
            }
        }

        public void StopAsync_WhenStopped_DoesNotThrow()
        {
            // Arrange
            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(new JobHostOptions()), new Mock<IJobHostContextFactory>().Object))
            {
                host.Start();
                host.Stop();

                // Act & Assert
                host.StopAsync().GetAwaiter().GetResult();
            }
        }

        public void StopAsync_WhenNotStarted_Throws()
        {
            // Arrange
            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(new JobHostOptions()), new Mock<IJobHostContextFactory>().Object))
            {
                // Act & Assert
                ExceptionAssert.ThrowsInvalidOperation(() => host.StopAsync(), "The host has not yet started.");
            }
        }

        public void StopAsync_WhenStarting_Throws()
        {
            // Arrange
            // TaskCompletionSource<IStorageAccount> getAccountTaskSource = new TaskCompletionSource<IStorageAccount>();
            JobHostOptions configuration = null; // CreateConfiguration(new LambdaStorageAccountProvider(
            //        (i1, i2) => getAccountTaskSource.Task));

            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(configuration), new Mock<IJobHostContextFactory>().Object))
            {
                Task starting = host.StartAsync();
                Assert.False(starting.IsCompleted); // Guard

                // Act & Assert
                ExceptionAssert.ThrowsInvalidOperation(() => host.StopAsync(), "The host has not yet started.");

                // Cleanup
                // getAccountTaskSource.SetResult(null);
                starting.GetAwaiter().GetResult();
            }
        }

        public void StopAsync_WhenWaiting_ReturnsIncompleteTask()
        {
            // Arrange
            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(new JobHostOptions()), new Mock<IJobHostContextFactory>().Object))
            {
                host.Start();

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

        public void StopAsync_WhenAlreadyStopping_ReturnsSameTask()
        {
            // Arrange
            JobHostOptions configuration = null;
            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(configuration), new Mock<IJobHostContextFactory>().Object))
            {
                host.Start();

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

        public void SimpleInvoke_WithDictionary()
        {
            var host = JobHostFactory.Create<ProgramSimple>(null);

            var x = "abc";
            ProgramSimple._value = null;
            host.Call("Test", new Dictionary<string, object> { { "value", x } });

            // Ensure test method was invoked properly.
            Assert.Equal(x, ProgramSimple._value);
        }

        public void SimpleInvoke_WithObject()
        {
            var host = JobHostFactory.Create<ProgramSimple>(null);

            var x = "abc";
            ProgramSimple._value = null;
            host.Call("Test", new { value = x });

            // Ensure test method was invoked properly.
            Assert.Equal(x, ProgramSimple._value);
        }

        public void CallAsyncWithCancellationToken_PassesCancellationTokenToMethod()
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

        public void Call_WhenMethodThrows_PreservesStackTrace()
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
                FunctionInvocationException exception = Assert.Throws<FunctionInvocationException>(
                    () => host.Call(methodInfo));
                Assert.Same(exception.InnerException, expectedException);
                Assert.NotNull(exception.InnerException.StackTrace);
                Assert.True(exception.InnerException.StackTrace.StartsWith(expectedStackTrace));
            }
            finally
            {
                ThrowingProgram.ExceptionInfo = null;
            }
        }

        public void BlobTrigger_ProvidesBlobTriggerBindingData()
        {
            try
            {
                // Arrange
                CloudStorageAccount account = CloudStorageAccount.DevelopmentStorageAccount;
                var host = JobHostFactory.Create<BlobTriggerBindingDataProgram>(account);
                MethodInfo methodInfo = typeof(BlobTriggerBindingDataProgram).GetMethod("OnBlob");
                string containerName = "a";
                string blobName = "b";
                string expectedPath = containerName + "/" + blobName;
                CloudBlobContainer container = account.CreateCloudBlobClient().GetContainerReference(containerName);
                ICloudBlob blob = container.GetBlockBlobReference(blobName);

                // Act
                host.Call(methodInfo, new { blob = blob });

                // Assert
                Assert.Equal(expectedPath, BlobTriggerBindingDataProgram.BlobTrigger);
            }
            finally
            {
                BlobTriggerBindingDataProgram.BlobTrigger = null;
            }
        }

        public void QueueTrigger_ProvidesQueueTriggerBindingData()
        {
            try
            {
                // Arrange
                var host = JobHostFactory.Create<QueueTriggerBindingDataProgram>(
                    CloudStorageAccount.DevelopmentStorageAccount);
                MethodInfo methodInfo = typeof(QueueTriggerBindingDataProgram).GetMethod("OnQueue");
                string expectedMessage = "a";

                // Act
                host.Call(methodInfo, new { message = expectedMessage });

                // Assert
                Assert.Equal(expectedMessage, QueueTriggerBindingDataProgram.QueueTrigger);
            }
            finally
            {
                QueueTriggerBindingDataProgram.QueueTrigger = null;
            }
        }

        public void QueueTrigger_WithTextualByteArrayMessage_ProvidesQueueTriggerBindingData()
        {
            try
            {
                // Arrange
                var host = JobHostFactory.Create<QueueTriggerBindingDataProgram>(
                    CloudStorageAccount.DevelopmentStorageAccount);
                MethodInfo methodInfo = typeof(QueueTriggerBindingDataProgram).GetMethod("OnQueue");
                string expectedMessage = "abc";
                CloudQueueMessage message = new CloudQueueMessage(expectedMessage);
                Assert.Equal(expectedMessage, message.AsString); // Guard

                // Act
                host.Call(methodInfo, new { message = message });

                // Assert
                Assert.Equal(expectedMessage, QueueTriggerBindingDataProgram.QueueTrigger);
            }
            finally
            {
                QueueTriggerBindingDataProgram.QueueTrigger = null;
            }
        }

        public void QueueTrigger_WithNonTextualByteArrayMessageUsingQueueTriggerBindingData_Throws()
        {
            try
            {
                // Arrange
                var host = JobHostFactory.Create<QueueTriggerBindingDataProgram>(
                    CloudStorageAccount.DevelopmentStorageAccount);
                MethodInfo methodInfo = typeof(QueueTriggerBindingDataProgram).GetMethod("OnQueue");
                byte[] contents = new byte[] { 0x00, 0xFF }; // Not valid UTF-8
                CloudQueueMessage message = CloudQueueMessage.CreateCloudQueueMessageFromByteArray(contents);

                // Act & Assert
                FunctionInvocationException exception = Assert.Throws<FunctionInvocationException>(
                    () => host.Call(methodInfo, new { message = message }));
                // This exeption shape/message could be better, but it's meets a minimum acceptibility threshold.
                Assert.Equal("Exception binding parameter 'queueTrigger'", exception.InnerException.Message);
                Exception innerException = exception.InnerException.InnerException;
                Assert.IsType<InvalidOperationException>(innerException);
                Assert.Equal("Binding data does not contain expected value 'queueTrigger'.", innerException.Message);
            }
            finally
            {
                QueueTriggerBindingDataProgram.QueueTrigger = null;
            }
        }

        public void QueueTrigger_WithNonTextualByteArrayMessageNotUsingQueueTriggerBindingData_DoesNotThrow()
        {
            try
            {
                // Arrange
                var host = JobHostFactory.Create<QueueTriggerBindingDataProgram>(
                    CloudStorageAccount.DevelopmentStorageAccount);
                MethodInfo methodInfo = typeof(QueueTriggerBindingDataProgram).GetMethod("ProcessQueueAsBytes");
                byte[] expectedBytes = new byte[] { 0x00, 0xFF }; // Not valid UTF-8
                CloudQueueMessage message = CloudQueueMessage.CreateCloudQueueMessageFromByteArray(expectedBytes);

                // Act
                host.Call(methodInfo, new { message = message });

                // Assert
                Assert.Equal(QueueTriggerBindingDataProgram.Bytes, expectedBytes);
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
                .ConfigureDefaultTestHost<BindingErrorsProgram>()
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

        /* $$$
        private class LambdaStorageAccountProvider : IStorageAccountProvider
        {
            private readonly Func<string, CancellationToken, Task<IStorageAccount>> _getAccountAsync;

            public LambdaStorageAccountProvider(Func<string, CancellationToken, Task<IStorageAccount>> getAccountAsync)
            {
                _getAccountAsync = getAccountAsync;
            }

            public string StorageConnectionString => throw new NotImplementedException();

            public string DashboardConnectionString => throw new NotImplementedException();

            public string InternalSasStorage => throw new NotImplementedException();

            public Task<IStorageAccount> TryGetAccountAsync(string connectionStringName,
                CancellationToken cancellationToken)
            {
                return _getAccountAsync.Invoke(connectionStringName, cancellationToken);
            }
        }*/ 

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

            public override void Log<TState>(Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
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
