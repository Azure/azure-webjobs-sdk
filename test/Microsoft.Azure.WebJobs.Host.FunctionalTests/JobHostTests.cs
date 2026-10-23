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
using Azure.Storage.Blobs;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            var provider = new LambdaJobHostContextFactory((a, b, c) => createTaskSource.Task);

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
                await oldListener.StopAsync(CancellationToken.None);

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
                await stopping;
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
                await host.StopAsync();
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
            var provider = new LambdaJobHostContextFactory((a, b, c) => createTaskSource.Task);

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
                await starting;
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
                await oldListener.StopAsync(CancellationToken.None);

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
                await stopping;
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
                await oldRunner.StopAsync(CancellationToken.None);

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
                await alreadyStopping;
                await stoppingAgain;
            }
        }

        [Fact]
        public async Task ShutdownFileCreated_HostShutsDownGracefully()
        {
            // Setup the directory where we'll be writing the shutdown file
            string shutdownPath = Path.Combine(Path.GetTempPath(), "WebJobs", "shutdown");
            Environment.SetEnvironmentVariable(Constants.AzureWebJobsShutdownFile, shutdownPath);
            Directory.CreateDirectory(Path.GetDirectoryName(shutdownPath));
            if (File.Exists(shutdownPath))
            {
                File.Delete(shutdownPath);
            }

            // Test hosted service that we'll use below to verify user registered hosted services
            // are stopped gracefully on shutdown
            var testService = new TestHostedService();

            var builder = new HostBuilder().ConfigureDefaultTestHost();
            builder.ConfigureServices(services =>
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService>(testService));
            });

            var host = builder.Build();
            using (host)
            {
                // start running the host without blocking so we can trigger the shutdown below
                Task runTask = host.RunAsync();

                // wait for the host to start completely
                var loggerProvider = host.GetTestLoggerProvider();
                IEnumerable<LogMessage> logs = null;
                await TestHelpers.Await(() =>
                {
                    logs = loggerProvider.GetAllLogMessages();
                    var startedLog = logs.SingleOrDefault(p => p.FormattedMessage == "Job host started");
                    return startedLog != null;
                });

                // trigger the shutdown
                File.Create(shutdownPath);

                // wait for the run task to complete successfully
                await TestHelpers.Await(() =>
                {
                    return runTask.IsCompleted;
                });

                Assert.True(testService.StartCalled);
                Assert.True(testService.StopCalled);

                logs = loggerProvider.GetAllLogMessages();
                Assert.NotNull(logs.SingleOrDefault(p => p.FormattedMessage == "Stopping JobHost"));
                Assert.NotNull(logs.SingleOrDefault(p => p.FormattedMessage == "Job host stopped"));
            }
        }

        [Fact]
        public async Task CallAsync_WithDictionary()
        {
            var host = JobHostFactory.Create<ProgramSimple>();

            var value = "abc";
            ProgramSimple._value = null;
            await host.CallAsync("Test", new Dictionary<string, object> { { "value", value } });

            // Ensure test method was invoked properly.
            Assert.Equal(value, ProgramSimple._value);
        }

        [Theory]
        [InlineData("Test")]
        [InlineData("test")]
        public async Task CallAsync_WithObject(string methodName)
        {
            var host = JobHostFactory.Create<ProgramSimple>();

            var x = "abc";
            ProgramSimple._value = null;
            await host.CallAsync(methodName, new { value = x });

            // Ensure test method was invoked properly.
            Assert.Equal(x, ProgramSimple._value);
        }

        [Fact]
        public async Task CallAsync_WithCancellationToken_PassesCancellationTokenToMethod()
        {
            // Arrange
            ProgramWithCancellationToken.Cleanup();
            var host = JobHostFactory.Create<ProgramWithCancellationToken>();

            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                ProgramWithCancellationToken.CancellationTokenSource = source;

                // Act
                await host.CallAsync("BindCancellationToken", null, source.Token);

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

                var host = JobHostFactory.Create<ThrowingProgram>();
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
                    b.AddAzureStorageBlobs();
                    b.AddAzureStorageQueues();
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

                // Skip validating the initial 'Starting JobHost' message and the OptionsFormatters

                // verify that the binding error was logged
                var bindingErrorLog = errorLogger.GetLogMessages().Single(l => l.FormattedMessage.Equals("Error indexing method 'BindingErrorsProgram.Invalid'"));
                Assert.NotNull(bindingErrorLog);
                Assert.Same(fex, bindingErrorLog.Exception);

                var disabledLog = errorLogger.GetLogMessages().Single(l => l.FormattedMessage.Equals("Function 'BindingErrorsProgram.Invalid' failed indexing and will be disabled."));
                Assert.NotNull(disabledLog);

                // verify that the valid function was still indexed
                Assert.Contains(errorLogger.GetLogMessages(), p => p.FormattedMessage.Contains("Found the following functions") && p.FormattedMessage.Contains("BindingErrorsProgram.Valid"));

                // verify that the job host was started successfully
                Assert.Contains(errorLogger.GetLogMessages(), p => p.FormattedMessage.Equals("Job host started"));

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
            private readonly Func<JobHost, CancellationToken, CancellationToken, Task<JobHostContext>> _create;

            public LambdaJobHostContextFactory(Func<JobHost, CancellationToken, CancellationToken, Task<JobHostContext>> create)
            {
                _create = create;
            }

            public Task<JobHostContext> Create(JobHost host, CancellationToken shutdownToken, CancellationToken cancellationToken)
            {
                return _create.Invoke(host, shutdownToken, cancellationToken);
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

        public class TestHostedService : IHostedService
        {
            public bool StartCalled { get; set; }
            public bool StopCalled { get; set; }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                StartCalled = true;
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                StopCalled = true;
                return Task.CompletedTask;
            }
        }
    }
}
