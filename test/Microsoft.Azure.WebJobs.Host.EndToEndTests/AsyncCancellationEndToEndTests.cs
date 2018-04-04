// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class AsyncCancellationEndToEndTests : IDisposable
    {
        private const string TestArtifactPrefix = "asynccancele2e";
        private const string QueueName = TestArtifactPrefix + "%rnd%";
        private const int DefaultTimeout = 5 * 1000;

        private static Action _invokeInFunction;
        private static bool _invokeInFunctionInvoked;
        private static EventWaitHandle _functionStarted;
        private static EventWaitHandle _functionCompleted;
        private static bool _tokenCancelled;

        private readonly CloudStorageAccount _storageAccount;
        private readonly RandomNameResolver _resolver;
        private readonly JobHostConfiguration _hostConfiguration;
        private static TestTraceWriter _traceWriter;

        public AsyncCancellationEndToEndTests()
        {
            _resolver = new RandomNameResolver();
            _traceWriter = new TestTraceWriter(TraceLevel.Verbose);

            _hostConfiguration = new JobHostConfiguration()
            {
                NameResolver = _resolver,
                TypeLocator = new FakeTypeLocator(typeof(AsyncCancellationEndToEndTests))
            };
            _hostConfiguration.Tracing.Tracers.Add(_traceWriter);

            _storageAccount = CloudStorageAccount.Parse(_hostConfiguration.StorageConnectionString);

            _invokeInFunction = () => { };
            _tokenCancelled = false;
            _functionStarted = new ManualResetEvent(initialState: false);
            _functionCompleted = new ManualResetEvent(initialState: false);
        }

        public void Dispose()
        {
            _functionStarted.Dispose();
            _functionCompleted.Dispose();

            if (_storageAccount != null)
            {
                CloudQueueClient queueClient = _storageAccount.CreateCloudQueueClient();
                foreach (var testQueue in queueClient.ListQueues(TestArtifactPrefix))
                {
                    testQueue.Delete();
                }
            }
        }


        [NoAutomaticTrigger]
        public static void InfiniteRunningFunctionUnlessCancelledManual(
            CancellationToken token)
        {
            AddLog($"Start {nameof(InfiniteRunningFunctionUnlessCancelledManual)}");
            FunctionBody(token);
            AddLog($"End {nameof(InfiniteRunningFunctionUnlessCancelledManual)}");
        }

        [NoAutomaticTrigger]
        public static void WriteQueueMessage(
            [Queue(QueueName)] out string message)
        {
            message = "test";
        }

        public static void InfiniteRunningFunctionUnlessCancelledTriggered(
            [QueueTrigger(QueueName)] string message,
            CancellationToken token)
        {
            FunctionBody(token);
        }

        private static void FunctionBody(CancellationToken token)
        {
            AddLog("FunctionBody");
            // If the token is cancelled here, something is not right
            if (!token.IsCancellationRequested)
            {
                _functionStarted.Set();

                AddLog("InvokeInFunction");
                _invokeInFunction();
                _invokeInFunctionInvoked = true;

                if (token.WaitHandle.WaitOne(DefaultTimeout))
                {
                    _tokenCancelled = token.IsCancellationRequested;
                }
            }

            _functionCompleted.Set();
            AddLog("FunctionComplete");
        }

        [Fact]
        public void WebJobsShutdown_WhenUsingHostCall_TriggersCancellationToken()
        {
            using (WebJobsShutdownContext shutdownContext = new WebJobsShutdownContext())
            using (JobHost host = new JobHost(_hostConfiguration))
            {
                _invokeInFunction = () => { shutdownContext.NotifyShutdown(); };

                Task callTask = InvokeNoAutomaticTriggerFunction(host);

                EvaluateNoAutomaticTriggerCancellation(callTask, expectedCancellation: true);
            }
        }

        [Fact]
        public void WebJobsShutdown_WhenUsingTriggeredFunction_TriggersCancellationToken()
        {
            using (WebJobsShutdownContext shutdownContext = new WebJobsShutdownContext())
            using (JobHost host = new JobHost(_hostConfiguration))
            {
                _invokeInFunction = () => { shutdownContext.NotifyShutdown(); };

                PrepareHostForTrigger(host, startHost: true);

                EvaluateTriggeredCancellation(expectedCancellation: true);
            }
        }

        [Fact]
        public void Stop_WhenUsingHostCall_DoesNotTriggerCancellationToken()
        {
            using (JobHost host = new JobHost(_hostConfiguration))
            {
                host.Start();

                Task callTask = InvokeNoAutomaticTriggerFunction(host);

                host.Stop();

                EvaluateNoAutomaticTriggerCancellation(callTask, expectedCancellation: false);
            }
        }

        [Fact]
        public void Stop_WhenUsingTriggeredFunction_TriggersCancellationToken()
        {
            using (JobHost host = new JobHost(_hostConfiguration))
            {
                PrepareHostForTrigger(host, startHost: true);

                host.Stop();

                EvaluateTriggeredCancellation(expectedCancellation: true);
            }
        }

        [Fact]
        public void Dispose_WhenUsingHostCall_TriggersCancellationToken()
        {
            Task callTask;

            using (JobHost host = new JobHost(_hostConfiguration))
            {
                callTask = InvokeNoAutomaticTriggerFunction(host);
            }

            EvaluateNoAutomaticTriggerCancellation(callTask, expectedCancellation: true);
        }

        [Fact]
        public void Dispose_WhenUsingTriggeredFunction_TriggersCancellationToken()
        {
            using (JobHost host = new JobHost(_hostConfiguration))
            {
                PrepareHostForTrigger(host, startHost: true);
            }

            EvaluateTriggeredCancellation(expectedCancellation: true);
        }

        [Fact]
        public void CallCancellationToken_WhenUsingHostCall_TriggersCancellationToken()
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            using (JobHost host = new JobHost(_hostConfiguration))
            {
                _invokeInFunction = () =>
                {
                    AddLog("Cancelling...");
                    tokenSource.Cancel();
                    AddLog("Canceled.");
                };

                Task callTask = InvokeNoAutomaticTriggerFunction(host, tokenSource.Token);

                EvaluateNoAutomaticTriggerCancellation(callTask, expectedCancellation: true);
            }
        }

        [Fact]
        public void CallCancellationToken_WhenUsingTriggeredFunction_DoesNotTriggerCancellationToken()
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            using (JobHost host = new JobHost(_hostConfiguration))
            {
                _invokeInFunction = () => { tokenSource.Cancel(); };

                PrepareHostForTrigger(host, startHost: false);
                Assert.True(host.StartAsync(tokenSource.Token).WaitUntilCompleted(2 * DefaultTimeout));

                EvaluateTriggeredCancellation(expectedCancellation: false);
            }
        }

        private void PrepareHostForTrigger(JobHost host, bool startHost)
        {
            host.Call(typeof(AsyncCancellationEndToEndTests).GetMethod("WriteQueueMessage"));

            if (startHost)
            {
                host.Start();
                Assert.True(_functionStarted.WaitOne(DefaultTimeout));
            }
        }

        private Task InvokeNoAutomaticTriggerFunction(JobHost host)
        {
            return InvokeNoAutomaticTriggerFunction(host, CancellationToken.None);
        }

        private Task InvokeNoAutomaticTriggerFunction(JobHost host, CancellationToken token)
        {
            AddLog("CallAsync...");
            Task callTask = host.CallAsync(
                typeof(AsyncCancellationEndToEndTests).GetMethod("InfiniteRunningFunctionUnlessCancelledManual"),
                token);
            AddLog("Called CallAsync. Waiting for _functionStarted");
            Assert.True(_functionStarted.WaitOne(DefaultTimeout), $"{string.Join(Environment.NewLine, _traceWriter.GetTraces())}");
            AddLog("_functionStarted");

            return callTask;
        }

        private void EvaluateTriggeredCancellation(bool expectedCancellation)
        {
            // Wait for the function to complete
            Assert.True(_functionCompleted.WaitOne(2 * DefaultTimeout));
            Assert.Equal(expectedCancellation, _tokenCancelled);
            Assert.True(_invokeInFunctionInvoked);
        }

        private void EvaluateNoAutomaticTriggerCancellation(Task task, bool expectedCancellation)
        {
            bool taskCompleted = task.WaitUntilCompleted(2 * DefaultTimeout);
            bool taskCompletedBeforeFunction = !_functionCompleted.WaitOne(0);

            Assert.True(taskCompleted);
            Assert.False(taskCompletedBeforeFunction);
            Assert.Equal(expectedCancellation, task.IsCanceled);
            Assert.Equal(expectedCancellation, _tokenCancelled);
            Assert.True(_invokeInFunctionInvoked);
        }

        private static void AddLog(string log)
        {
            _traceWriter.Info($"[Thread {Thread.CurrentThread.ManagedThreadId}] {log}");
        }
    }
}
