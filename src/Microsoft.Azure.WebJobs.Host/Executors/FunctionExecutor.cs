// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class FunctionExecutor : IFunctionExecutor, IFunctionActivityStatusProvider, IRetryNotifier
    {
        private readonly IFunctionInstanceLogger _functionInstanceLogger;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly IAsyncCollector<FunctionInstanceLogEntry> _functionEventCollector;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _resultsLogger;
        private readonly IEnumerable<IFunctionFilter> _globalFunctionFilters;
        private readonly IDrainModeManager _drainModeManager;
        private readonly ConcurrencyManager _concurrencyManager;
        private int _outstandingInvocations;
        private int _outstandingRetries;

        private readonly Dictionary<string, object> _inputBindingScope = new Dictionary<string, object>
        {
            [LogConstants.CategoryNameKey] = LogCategories.Bindings,
            [LogConstants.LogLevelKey] = LogLevel.Information
        };

        private readonly Dictionary<string, object> _outputBindingScope = new Dictionary<string, object>
        {
            [LogConstants.CategoryNameKey] = LogCategories.Bindings,
            [LogConstants.LogLevelKey] = LogLevel.Information
        };

        private readonly IFunctionOutputLogger _functionOutputLogger;
        private HostOutputMessage _hostOutputMessage;

        public FunctionExecutor(
                IFunctionInstanceLogger functionInstanceLogger,
                IFunctionOutputLogger functionOutputLogger,
                IWebJobsExceptionHandler exceptionHandler,
                IAsyncCollector<FunctionInstanceLogEntry> functionEventCollector,
                ConcurrencyManager concurrencyManager,
                ILoggerFactory loggerFactory = null,
                IEnumerable<IFunctionFilter> globalFunctionFilters = null,
                IDrainModeManager drainModeManager = null)
        {
            _functionInstanceLogger = functionInstanceLogger ?? throw new ArgumentNullException(nameof(functionInstanceLogger));
            _functionOutputLogger = functionOutputLogger;
            _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
            _functionEventCollector = functionEventCollector ?? throw new ArgumentNullException(nameof(functionEventCollector));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _resultsLogger = _loggerFactory.CreateLogger(LogCategories.Results);
            _globalFunctionFilters = globalFunctionFilters ?? Enumerable.Empty<IFunctionFilter>();
            _drainModeManager = drainModeManager;
            _concurrencyManager = concurrencyManager ?? throw new ArgumentNullException(nameof(concurrencyManager));
        }

        public HostOutputMessage HostOutputMessage
        {
            get { return _hostOutputMessage; }
            set { _hostOutputMessage = value; }
        }

        public async Task<IDelayedException> TryExecuteAsync(IFunctionInstance functionInstance, CancellationToken cancellationToken)
        {
            var functionInstanceEx = (IFunctionInstanceEx)functionInstance;
            var logger = _loggerFactory.CreateLogger(LogCategories.CreateFunctionCategory(functionInstanceEx.FunctionDescriptor.LogName));
            var functionStartedMessage = CreateStartedMessage(functionInstanceEx);
            var parameterHelper = new ParameterHelper(functionInstanceEx);
            ExceptionDispatchInfo exceptionInfo = null;
            string functionStartedMessageId = null;
            FunctionInstanceLogEntry instanceLogEntry = null;
            Stopwatch sw = Stopwatch.StartNew();
            string concurrencyFunctionId = functionInstanceEx.FunctionDescriptor.SharedListenerId ?? functionInstanceEx.FunctionDescriptor.Id;
            
            try
            {
                Interlocked.Increment(ref _outstandingInvocations);
                if (_concurrencyManager.Enabled)
                {
                    _concurrencyManager.FunctionStarted(concurrencyFunctionId);
                }

                using (_resultsLogger?.BeginFunctionScope(functionInstanceEx, HostOutputMessage.HostInstanceId))
                using (parameterHelper)
                {
                    try
                    {
                        parameterHelper.Initialize();
                        instanceLogEntry = CreateInstanceLogEntry(functionStartedMessage);
                        await _functionEventCollector.AddAsync(instanceLogEntry);

                        functionStartedMessageId = await ExecuteWithLoggingAsync(functionInstanceEx, functionStartedMessage, instanceLogEntry, parameterHelper, logger, cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        functionStartedMessage.Failure = FunctionFailure.FromException(exception);
                        exceptionInfo = ExceptionDispatchInfo.Capture(exception);
                        exceptionInfo = await InvokeExceptionFiltersAsync(parameterHelper.JobInstance, exceptionInfo, functionInstanceEx, parameterHelper.FilterContextProperties, logger, cancellationToken);
                    }
                    finally
                    {
                        functionStartedMessage.ParameterLogs = parameterHelper.ParameterLogCollector;
                        functionStartedMessage.EndTime = DateTimeOffset.UtcNow;
                    }

                    // If function started was logged, don't cancel calls to log function completed.
                    bool loggedStartedEvent = functionStartedMessageId != null;
                    _functionInstanceLogger.LogFunctionCompleted(functionStartedMessage);

                    if (instanceLogEntry != null)
                    {
                        CompleteInstanceLogEntry(instanceLogEntry, functionStartedMessage.Arguments, exceptionInfo);
                        await _functionEventCollector.AddAsync(instanceLogEntry);
                        _resultsLogger?.LogFunctionResult(instanceLogEntry);
                    }

                    if (loggedStartedEvent)
                    {
                        _functionInstanceLogger.DeleteLogFunctionStarted(functionStartedMessageId);
                    }
                }

                if (exceptionInfo != null)
                {
                    await HandleExceptionAsync(functionInstanceEx.FunctionDescriptor.TimeoutAttribute, exceptionInfo, _exceptionHandler);
                }
            }
            finally
            {
                sw.Stop();
                if (_concurrencyManager.Enabled)
                {
                    _concurrencyManager.FunctionCompleted(concurrencyFunctionId, sw.Elapsed);
                }

                Interlocked.Decrement(ref _outstandingInvocations);

                ((IDisposable)functionInstanceEx)?.Dispose();
            }

            return exceptionInfo != null ? new ExceptionDispatchInfoDelayedException(exceptionInfo) : null;
        }

        public FunctionActivityStatus GetStatus()
        {
            return new FunctionActivityStatus()
            {
                OutstandingInvocations = _outstandingInvocations,
                OutstandingRetries = _outstandingRetries
            };
        }

        public void RetryPending()
        {
            Interlocked.Increment(ref _outstandingRetries);
        }

        public void RetryComplete()
        {
            Interlocked.Decrement(ref _outstandingRetries);
        }

        private FunctionInstanceLogEntry CreateInstanceLogEntry(FunctionCompletedMessage functionStartedMessage)
        {
            var logEntry = new FunctionInstanceLogEntry
            {
                FunctionInstanceId = functionStartedMessage.FunctionInstanceId,
                ParentId = functionStartedMessage.ParentId,
                FunctionName = functionStartedMessage.Function.ShortName,
                LogName = functionStartedMessage.Function.LogName,
                TriggerReason = functionStartedMessage.ReasonDetails,
                StartTime = functionStartedMessage.StartTime.DateTime,
                Properties = new Dictionary<string, object>(),
                LiveTimer = Stopwatch.StartNew()
            };
            Debug.Assert(logEntry.IsStart);

            return logEntry;
        }

        private void CompleteInstanceLogEntry(FunctionInstanceLogEntry instanceLogEntry, IDictionary<string, string> arguments, ExceptionDispatchInfo exceptionInfo)
        {
            instanceLogEntry.LiveTimer.Stop();
            instanceLogEntry.EndTime = DateTime.UtcNow;
            instanceLogEntry.Duration = instanceLogEntry.LiveTimer.Elapsed;
            instanceLogEntry.Arguments = arguments;
            if (exceptionInfo != null)
            {
                var ex = exceptionInfo.SourceException;
                instanceLogEntry.Exception = ex;
                if (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                }
                instanceLogEntry.ErrorDetails = ex.Message;
            }
        }

        private async Task<ExceptionDispatchInfo> InvokeExceptionFiltersAsync(object jobInstance, ExceptionDispatchInfo exceptionDispatchInfo, IFunctionInstance functionInstance,
            IDictionary<string, object> properties, ILogger logger, CancellationToken cancellationToken)
        {
            var exceptionFilters = GetFilters<IFunctionExceptionFilter>(_globalFunctionFilters, functionInstance.FunctionDescriptor, jobInstance);
            if (exceptionFilters.Any())
            {
                var exceptionContext = new FunctionExceptionContext(functionInstance.Id, functionInstance.FunctionDescriptor.ShortName, logger, exceptionDispatchInfo, properties);
                Exception exception = exceptionDispatchInfo.SourceException;
                foreach (var exceptionFilter in exceptionFilters)
                {
                    try
                    {
                        await exceptionFilter.OnExceptionAsync(exceptionContext, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // if a filter throws, we capture that error to pass to subsequent filters
                        exception = ex;
                        exceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);
                        exceptionContext.ExceptionDispatchInfo = exceptionDispatchInfo;
                    }
                }
            }

            return exceptionDispatchInfo;
        }

        internal static async Task HandleExceptionAsync(TimeoutAttribute timeout, ExceptionDispatchInfo exceptionInfo, IWebJobsExceptionHandler exceptionHandler)
        {
            if (exceptionInfo.SourceException == null)
            {
                return;
            }

            Exception exception = exceptionInfo.SourceException;

            if (exception.IsTimeout())
            {
                await exceptionHandler.OnTimeoutExceptionAsync(exceptionInfo, timeout.GracePeriod);
            }
            else if (exception.IsFatal())
            {
                await exceptionHandler.OnUnhandledExceptionAsync(exceptionInfo);
            }
        }

        internal async Task<string> ExecuteWithLoggingAsync(IFunctionInstanceEx instance, FunctionStartedMessage message,
            FunctionInstanceLogEntry instanceLogEntry, ParameterHelper parameterHelper, ILogger logger, CancellationToken cancellationToken)
        {
            var outputDefinition = await _functionOutputLogger.CreateAsync(instance, cancellationToken);
            var outputLog = outputDefinition.CreateOutput();
            var updateOutputLogTimer = StartOutputTimer(outputLog.UpdateCommand, _exceptionHandler);

            try
            {
                // Create a linked token source that will allow us to signal function cancellation
                // (e.g. Based on TimeoutAttribute, etc.)                
                CancellationTokenSource functionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                using (functionCancellationTokenSource)
                {
                    FunctionOutputLogger.SetOutput(outputLog);

                    // Must bind before logging (bound invoke string is included in log message).
                    var functionContext = new FunctionBindingContext(instance, functionCancellationTokenSource.Token);
                    var valueBindingContext = new ValueBindingContext(functionContext, cancellationToken);

                    using (logger.BeginScope(_inputBindingScope))
                    {
                        var valueProviders = await instance.BindingSource.BindAsync(valueBindingContext);
                        parameterHelper.SetValueProviders(valueProviders);
                    }

                    // Log started events
                    CompleteStartedMessage(message, outputDefinition, parameterHelper);
                    string startedMessageId = _functionInstanceLogger.LogFunctionStarted(message);

                    instanceLogEntry.Arguments = message.Arguments;
                    await _functionEventCollector.AddAsync(instanceLogEntry);

                    Exception invocationException = null;
                    ITaskSeriesTimer updateParameterLogTimer = null;
                    try
                    {
                        if (outputDefinition != NullFunctionOutputDefinition.Instance)
                        {
                            var parameterWatchers = parameterHelper.CreateParameterWatchers();
                            var updateParameterLogCommand = outputDefinition.CreateParameterLogUpdateCommand(parameterWatchers, logger);
                            updateParameterLogTimer = StartParameterLogTimer(updateParameterLogCommand, _exceptionHandler);
                        }

                        await ExecuteWithWatchersAsync(instance, parameterHelper, logger, functionCancellationTokenSource);

                        if (updateParameterLogTimer != null)
                        {
                            // Stop the watches after calling IValueBinder.SetValue (it may do things that should show up in
                            // the watches).
                            // Also, IValueBinder.SetValue could also take a long time (flushing large caches), and so it's
                            // useful to have watches still running.
                            await updateParameterLogTimer.StopAsync(functionCancellationTokenSource.Token);
                        }
                    }
                    catch (Exception ex)
                    {
                        invocationException = ex;
                    }
                    finally
                    {
                        updateParameterLogTimer?.Dispose();
                        parameterHelper.FlushParameterWatchers();
                    }

                    var exceptionInfo = GetExceptionDispatchInfo(invocationException, instance);
                    if (exceptionInfo == null && updateOutputLogTimer != null)
                    {
                        await updateOutputLogTimer.StopAsync(cancellationToken);
                    }

                    // We save the exception info above rather than throwing to ensure we always write
                    // console output even if the function fails or was canceled.
                    if (outputLog != null)
                    {
                        await outputLog.SaveAndCloseAsync(instanceLogEntry, cancellationToken);
                    }

                    if (exceptionInfo != null)
                    {
                        if (parameterHelper.HasSingleton)
                        {
                            // release any held singleton lock immediately
                            SingletonLock singleton = await parameterHelper.GetSingletonLockAsync();
                            if (singleton != null && singleton.IsHeld)
                            {
                                await singleton.ReleaseAsync(CancellationToken.None);
                            }
                        }

                        exceptionInfo.Throw();
                    }
                    return startedMessageId;
                }
            }
            finally
            {
                if (outputLog != null)
                {
                    outputLog.Dispose();
                }
                if (updateOutputLogTimer != null)
                {
                    updateOutputLogTimer.Dispose();
                }
            }
        }

        private ExceptionDispatchInfo GetExceptionDispatchInfo(Exception invocationException, IFunctionInstanceEx functionInstance)
        {
            ExceptionDispatchInfo exceptionInfo = null;

            if (invocationException != null)
            {
                // In the event of cancellation or timeout, we use the original exception without additional logging.
                if (invocationException is OperationCanceledException || invocationException is FunctionTimeoutException)
                {
                    exceptionInfo = ExceptionDispatchInfo.Capture(invocationException);
                }
                else
                {
                    string errorMessage = string.Format("Exception while executing function: {0}", functionInstance.FunctionDescriptor.ShortName);
                    FunctionInvocationException fex = new FunctionInvocationException(errorMessage, functionInstance.Id, functionInstance.FunctionDescriptor.FullName, invocationException);
                    exceptionInfo = ExceptionDispatchInfo.Capture(fex);
                }
            }

            return exceptionInfo;
        }

        /// <summary>
        /// If the specified function instance requires a timeout (via <see cref="TimeoutAttribute"/>),
        /// create and start the timer.
        /// </summary>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        internal static System.Timers.Timer StartFunctionTimeout(IFunctionInstance instance, TimeoutAttribute attribute,
            CancellationTokenSource cancellationTokenSource, ILogger logger)
        {
            if (attribute == null || attribute.Timeout <= TimeSpan.Zero)
            {
                return null;
            }

            TimeSpan timeout = attribute.Timeout;
            bool usingCancellationToken = instance.FunctionDescriptor.HasCancellationToken;
            if (!usingCancellationToken && !attribute.ThrowOnTimeout)
            {
                // function doesn't bind to the CancellationToken and we will not throw if it fires,
                // so no point in setting up the cancellation timer
                return null;
            }

            // Create a Timer that will cancel the token source when it fires. We're using our
            // own Timer (rather than CancellationToken.CancelAfter) so we can write a log entry
            // before cancellation occurs.
            var timer = new System.Timers.Timer(timeout.TotalMilliseconds)
            {
                AutoReset = false
            };

            timer.Elapsed += (o, e) =>
            {
                OnFunctionTimeout(timer, instance.FunctionDescriptor, instance.Id, timeout, attribute.TimeoutWhileDebugging, logger, cancellationTokenSource, () => Debugger.IsAttached);
            };

            timer.Start();

            return timer;
        }

        internal static void OnFunctionTimeout(System.Timers.Timer timer, FunctionDescriptor method, Guid instanceId, TimeSpan timeout, bool timeoutWhileDebugging,
            ILogger logger, CancellationTokenSource cancellationTokenSource, Func<bool> isDebuggerAttached)
        {
            timer.Stop();

            bool shouldTimeout = timeoutWhileDebugging || !isDebuggerAttached();
            string message = string.Format(CultureInfo.InvariantCulture,
                "Timeout value of {0} exceeded by function '{1}' (Id: '{2}'). {3}",
                timeout.ToString(), method.ShortName, instanceId,
                shouldTimeout ? "Initiating cancellation." : "Function will not be cancelled while debugging.");

            logger?.LogError(message);

            // Only cancel the token if not debugging
            if (shouldTimeout)
            {
                // only cancel the token AFTER we've logged our error, since
                // the Dashboard function output is also tied to this cancellation
                // token and we don't want to dispose the logger prematurely.
                cancellationTokenSource.Cancel();
            }
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private static ITaskSeriesTimer StartOutputTimer(IRecurrentCommand updateCommand, IWebJobsExceptionHandler exceptionHandler)
        {
            if (updateCommand == null)
            {
                return null;
            }

            TimeSpan initialDelay = FunctionOutputIntervals.InitialDelay;
            TimeSpan refreshRate = FunctionOutputIntervals.RefreshRate;
            ITaskSeriesTimer timer = FixedDelayStrategy.CreateTimer(updateCommand, initialDelay, refreshRate, exceptionHandler);
            timer.Start();

            return timer;
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private static ITaskSeriesTimer StartParameterLogTimer(IRecurrentCommand updateCommand, IWebJobsExceptionHandler exceptionHandler)
        {
            if (updateCommand == null)
            {
                return null;
            }

            TimeSpan initialDelay = FunctionParameterLogIntervals.InitialDelay;
            TimeSpan refreshRate = FunctionParameterLogIntervals.RefreshRate;
            ITaskSeriesTimer timer = FixedDelayStrategy.CreateTimer(updateCommand, initialDelay, refreshRate, exceptionHandler);
            timer.Start();

            return timer;
        }

        internal async Task ExecuteWithWatchersAsync(IFunctionInstanceEx instance,
            ParameterHelper parameterHelper,
            ILogger logger,
            CancellationTokenSource functionCancellationTokenSource)
        {
            await parameterHelper.PrepareParametersAsync();

            SingletonLock singleton = null;
            if (parameterHelper.HasSingleton)
            {
                // if the function is a Singleton, acquire the lock
                singleton = await parameterHelper.GetSingletonLockAsync();
                await singleton.AcquireAsync(functionCancellationTokenSource.Token);
            }

            using (CancellationTokenSource timeoutTokenSource = new CancellationTokenSource())
            {
                var timeoutAttribute = instance.FunctionDescriptor.TimeoutAttribute;
                bool throwOnTimeout = timeoutAttribute?.ThrowOnTimeout ?? false;
                var timer = StartFunctionTimeout(instance, timeoutAttribute, timeoutTokenSource, logger);
                TimeSpan timerInterval = timer == null ? TimeSpan.MinValue : TimeSpan.FromMilliseconds(timer.Interval);

                try
                {
                    using (logger.BeginScope(new Dictionary<string, object>
                    {
                        [LogConstants.CategoryNameKey] = LogCategories.CreateFunctionCategory(instance.FunctionDescriptor.LogName),
                        [LogConstants.LogLevelKey] = LogLevel.Information
                    }))
                    {
                        var filters = GetFilters<IFunctionInvocationFilter>(_globalFunctionFilters, instance.FunctionDescriptor, parameterHelper.JobInstance);
                        var invoker = FunctionInvocationFilterInvoker.Create(parameterHelper.Invoker, filters, instance, parameterHelper, logger);

                        object returnValue = null;
                        if (timer == null)
                        {
                            returnValue = await invoker.InvokeAsync(parameterHelper.JobInstance, parameterHelper.InvokeParameters);
                        }
                        else
                        {
                            returnValue = await InvokeWithTimeoutAsync(invoker, parameterHelper, timeoutTokenSource, functionCancellationTokenSource, throwOnTimeout, timerInterval, instance);
                        }
                        parameterHelper.SetReturnValue(returnValue);
                    }
                }
                finally
                {
                    if (timer != null)
                    {
                        timer.Stop();
                        timer.Dispose();
                    }
                }
            }

            using (logger.BeginScope(_outputBindingScope))
            {
                await parameterHelper.ProcessOutputParameters(functionCancellationTokenSource.Token);
            }

            if (singleton != null)
            {
                await singleton.ReleaseAsync(functionCancellationTokenSource.Token);
            }
        }

        internal static async Task<object> InvokeWithTimeoutAsync(IFunctionInvoker invoker, ParameterHelper parameterHelper, CancellationTokenSource timeoutTokenSource,
            CancellationTokenSource functionCancellationTokenSource, bool throwOnTimeout, TimeSpan timerInterval, IFunctionInstance instance)
        {
            Task<object> invokeTask = invoker.InvokeAsync(parameterHelper.JobInstance, parameterHelper.InvokeParameters);

            if (timerInterval > TimeSpan.Zero)
            {
                // There are three ways the function can complete:
                //   1. The invokeTask itself completes first.
                //   2. A cancellation is requested (by host.Stop(), for example).
                //      a. Continue waiting for the invokeTask to complete. Either #1 or #3 will occur.
                //   3. A timeout fires.
                //      a. If throwOnTimeout, we throw the FunctionTimeoutException.
                //      b. If !throwOnTimeout, wait for the task to complete.

                // Combine #1 and #2 with a timeout task (handled by this method).
                // functionCancellationTokenSource.Token is passed to each function that requests it, so we need to call Cancel() on it
                // if there is a timeout.
                bool isTimeout = await TryHandleTimeoutAsync(invokeTask, functionCancellationTokenSource.Token, throwOnTimeout, timeoutTokenSource.Token,
                    timerInterval, instance, () => functionCancellationTokenSource.Cancel());

                // #2 occurred. If we're going to throwOnTimeout, watch for a timeout while we wait for invokeTask to complete.
                if (throwOnTimeout && !isTimeout && functionCancellationTokenSource.IsCancellationRequested)
                {
                    await TryHandleTimeoutAsync(invokeTask, CancellationToken.None, throwOnTimeout, timeoutTokenSource.Token, timerInterval, instance, null);
                }
            }

            object returnValue = await invokeTask;

            return returnValue;
        }

        /// <summary>
        /// Returns the list of filters in the order they should be executed in.
        /// Filter order is decided by the scope at which the filter is declared.
        /// The scopes (in order) are: Instance, Global, Class, Method.
        /// The execution model is "Russian Doll" - Instance filters surround Global filters
        /// which surround Class filters which surround Method filters. As a result of this nesting,
        /// for filters with pre/post methods (e.g. <see cref="IFunctionInvocationFilter"/>) the executing portion
        /// of the filters runs in the reverse order.
        /// </summary>
        private static IEnumerable<TFilter> GetFilters<TFilter>(IEnumerable<IFunctionFilter> globalFunctionFilters, FunctionDescriptor functionDescriptor, object instance) where TFilter : class, IFunctionFilter
        {
            // If the job method is an instance method and the job class implements
            // the filter interface, this filter runs first before all other filters
            if (instance is TFilter instanceFilter)
            {
                yield return instanceFilter;
            }

            // Add any global filters
            foreach (var filter in globalFunctionFilters)
            {
                if (filter is TFilter tFilter)
                {
                    yield return tFilter;
                }
            }

            // Next, any class level filters are added
            foreach (var filter in functionDescriptor.ClassLevelFilters)
            {
                if (filter is TFilter tFilter)
                {
                    yield return tFilter;
                }
            }

            // Finally, any method level filters are added
            foreach (var filter in functionDescriptor.MethodLevelFilters)
            {
                if (filter is TFilter tFilter)
                {
                    yield return tFilter;
                }
            }
        }

        /// <summary>
        /// Executes a timeout pattern. Throws an exception if the timeoutToken is canceled before taskToTimeout completes and throwOnTimeout is true.
        /// </summary>
        /// <param name="invokeTask">The task to run.</param>
        /// <param name="shutdownToken">A token that is canceled if a host shutdown is requested.</param>
        /// <param name="throwOnTimeout">True if the method should throw an OperationCanceledException if it times out.</param>
        /// <param name="timeoutToken">The token to watch. If it is canceled, taskToTimeout has timed out.</param>
        /// <param name="timeoutInterval">The timeout period. Used only in the exception message.</param>
        /// <param name="instance">The function instance. Used only in the exceptionMessage</param>
        /// <param name="onTimeout">A callback to be executed if a timeout occurs.</param>
        /// <returns>True if a timeout occurred. Otherwise, false.</returns>
        private static async Task<bool> TryHandleTimeoutAsync(Task invokeTask,
            CancellationToken shutdownToken, bool throwOnTimeout, CancellationToken timeoutToken,
            TimeSpan timeoutInterval, IFunctionInstance instance, Action onTimeout)
        {
            Task timeoutTask = Task.Delay(-1, timeoutToken);
            Task shutdownTask = Task.Delay(-1, shutdownToken);
            Task completedTask = await Task.WhenAny(invokeTask, shutdownTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                if (onTimeout != null)
                {
                    onTimeout();
                }

                if (throwOnTimeout)
                {
                    // If we need to throw, throw now. This will bubble up and eventually bring down the host after
                    // a short grace period for the function to handle the cancellation.
                    string errorMessage = string.Format("Timeout value of {0} was exceeded by function: {1}", timeoutInterval, instance.FunctionDescriptor.ShortName);
                    throw new FunctionTimeoutException(errorMessage, instance.Id, instance.FunctionDescriptor.ShortName, timeoutInterval, invokeTask, null);
                }

                return true;
            }
            return false;
        }

        private FunctionCompletedMessage CreateStartedMessage(IFunctionInstance instance)
        {
            // A FunctionCompletedMessage ISA FunctionStartedMessage. To avoid an extra object allocation
            // later, we return a completed message. Initally only the "started" portions of the message
            // are populated, then later when the invocation completes, the "completed" portions will
            // be filled out.
            var message = new FunctionCompletedMessage
            {
                HostInstanceId = _hostOutputMessage.HostInstanceId,
                HostDisplayName = _hostOutputMessage.HostDisplayName,
                SharedQueueName = _hostOutputMessage.SharedQueueName,
                InstanceQueueName = _hostOutputMessage.InstanceQueueName,
                Heartbeat = _hostOutputMessage.Heartbeat,
                WebJobRunIdentifier = _hostOutputMessage.WebJobRunIdentifier,
                FunctionInstanceId = instance.Id,
                Function = instance.FunctionDescriptor,
                ParentId = instance.ParentId,
                TriggerDetails = instance.TriggerDetails,
                Reason = instance.Reason,
                StartTime = DateTimeOffset.UtcNow
            };

            // It's important that the host formats the reason before sending the message.
            // This enables extensibility scenarios. For the built in types, the Host and Dashboard
            // share types so it's possible (in the case of triggered functions) for the formatting
            // to require a call to TriggerParameterDescriptor.GetTriggerReason and that can only
            // be done on the Host side in the case of extensions (since the dashboard doesn't
            // know about extension types).
            message.ReasonDetails = message.FormatReason();

            return message;
        }

        private static void CompleteStartedMessage(FunctionStartedMessage message, IFunctionOutputDefinition outputDefinition, ParameterHelper parameterHelper)
        {
            message.OutputBlob = outputDefinition.OutputBlob;
            message.ParameterLogBlob = outputDefinition.ParameterLogBlob;
            message.Arguments = parameterHelper.CreateInvokeStringArguments();
        }

        // Handle various phases of parameter building and logging.
        // The paramerter phases are: 
        //  1. Initial binding data from the trigger. 
        //  2. IValueProvider[]. Provides a System.Object along with additional information (liking logging) 
        //  3. System.Object[]. which can be passed to the actual MethodInfo for execution 
        internal class ParameterHelper : IDisposable
        {
            private readonly IFunctionInstanceEx _functionInstance;

            // Logs, contain the result from invoking the IWatchers.
            private IDictionary<string, ParameterLog> _parameterLogCollector;

            // Optional runtime watchers for the parameters. 
            private IReadOnlyDictionary<string, IWatcher> _parameterWatchers;

            // ValueProviders for the parameters. These are produced from binding. 
            // This includes a possible $return for the return value. 
            private IReadOnlyDictionary<string, IValueProvider> _valueProviders;

            // state bag passed to all function filters
            private IDictionary<string, object> _filterContextProperties;

            private object _returnValue;

            private IValueProvider _singletonValueProvider;

            private bool _disposed;

            // for mock testing
            public ParameterHelper()
            {
            }

            public ParameterHelper(IFunctionInstanceEx functionInstance)
            {
                if (functionInstance == null)
                {
                    throw new ArgumentNullException(nameof(functionInstance));
                }

                _functionInstance = functionInstance;
            }

            // Phsyical objects to pass to the underlying method Info. These will get updated for out-parameters. 
            // These are produced by executing the binders. 
            public object[] InvokeParameters { get; internal set; }

            public object JobInstance { get; set; }

            public bool HasSingleton => _singletonValueProvider != null;

            public IDictionary<string, ParameterLog> ParameterLogCollector
            {
                get
                {
                    if (_parameterLogCollector == null)
                    {
                        _parameterLogCollector = new Dictionary<string, ParameterLog>();
                    }
                    return _parameterLogCollector;
                }
            }

            public object ReturnValue => _returnValue;

            public IFunctionInvokerEx Invoker { get; private set; }

            public IDictionary<string, object> FilterContextProperties
            {
                get
                {
                    if (_filterContextProperties == null)
                    {
                        _filterContextProperties = new Dictionary<string, object>();
                    }
                    return _filterContextProperties;
                }
            }

            public void Initialize()
            {
                Invoker = _functionInstance.GetFunctionInvoker();
                JobInstance = Invoker.CreateInstance(_functionInstance);
            }

            // Convert the parameters and their names to a dictionary
            public Dictionary<string, object> GetParametersAsDictionary()
            {
                Dictionary<string, object> parametersAsDictionary = new Dictionary<string, object>();

                int counter = 0;
                foreach (var name in _functionInstance.Invoker.ParameterNames)
                {
                    parametersAsDictionary[name] = InvokeParameters[counter];
                    counter++;
                }

                return parametersAsDictionary;
            }

            // ValueProviders for the parameters. These are produced from binding. 
            // This includes a possible $return for the return value. 
            public void SetValueProviders(IReadOnlyDictionary<string, IValueProvider> valueProviders)
            {
                _valueProviders = valueProviders;

                if (_valueProviders != null)
                {
                    if (_valueProviders.TryGetValue(SingletonValueProvider.SingletonParameterName, out IValueProvider valueProvider))
                    {
                        _singletonValueProvider = valueProvider;
                    }
                }
            }

            public IReadOnlyDictionary<string, IWatcher> CreateParameterWatchers()
            {
                if (_parameterWatchers != null)
                {
                    return _parameterWatchers;
                }

                var watchers = new Dictionary<string, IWatcher>();
                foreach (KeyValuePair<string, IValueProvider> item in _valueProviders)
                {
                    var watchable = item.Value as IWatchable;
                    if (watchable != null)
                    {
                        watchers.Add(item.Key, watchable.Watcher);
                    }
                }

                _parameterWatchers = watchers;
                return watchers;
            }

            public void FlushParameterWatchers()
            {
                if (_parameterWatchers == null)
                {
                    return;
                }

                foreach (KeyValuePair<string, IWatcher> item in _parameterWatchers)
                {
                    IWatcher watch = item.Value;
                    if (watch == null)
                    {
                        continue;
                    }

                    ParameterLog status = watch.GetStatus();
                    if (status == null)
                    {
                        continue;
                    }

                    ParameterLogCollector.Add(item.Key, status);
                }
            }

            public IDictionary<string, string> CreateInvokeStringArguments()
            {
                IDictionary<string, string> arguments = new Dictionary<string, string>(_valueProviders?.Count ?? 0);

                if (_valueProviders != null)
                {
                    foreach (KeyValuePair<string, IValueProvider> parameter in _valueProviders)
                    {
                        arguments.Add(parameter.Key, parameter.Value?.ToInvokeString() ?? "null");
                    }
                }

                return arguments;
            }

            // Run the IValueProviders to create the real set of underlying objects that we'll pass to the method. 
            public async Task PrepareParametersAsync()
            {
                var parameterNames = _functionInstance.Invoker.ParameterNames;
                object[] reflectionParameters = new object[parameterNames.Count];
                List<Exception> bindingExceptions = new List<Exception>();

                for (int index = 0; index < parameterNames.Count; index++)
                {
                    string name = parameterNames[index];
                    IValueProvider provider = _valueProviders[name];

                    var exceptionProvider = provider as BindingExceptionValueProvider;
                    if (exceptionProvider != null)
                    {
                        bindingExceptions.Add(exceptionProvider.Exception);
                    }

                    reflectionParameters[index] = await provider.GetValueAsync();
                }

                IDelayedException delayedBindingException = null;
                if (bindingExceptions.Count == 1)
                {
                    delayedBindingException = new DelayedException(bindingExceptions[0]);
                }
                else if (bindingExceptions.Count > 1)
                {
                    delayedBindingException = new DelayedException(new AggregateException(bindingExceptions));
                }

                if (delayedBindingException != null)
                {
                    // This is done inside a watcher context so that each binding error is publish next to the binding in
                    // the parameter status log.
                    delayedBindingException.Throw();
                }

                InvokeParameters = reflectionParameters;
            }

            // Retrieve the function singleton lock from the parameters. 
            // Null if not found. 
            public async Task<SingletonLock> GetSingletonLockAsync()
            {
                if (_singletonValueProvider == null)
                {
                    return null;
                }
                return (SingletonLock)(await _singletonValueProvider.GetValueAsync());
            }

            // Process any out parameters and persist any pending values.
            // Ensure IValueBinder.SetValue is called in BindStepOrder. This ordering is particularly important for
            // ensuring queue outputs occur last. That way, all other function side-effects are guaranteed to have
            // occurred by the time messages are enqueued.
            public async Task ProcessOutputParameters(CancellationToken cancellationToken)
            {
                string[] parameterNamesInBindOrder = SortParameterNamesInStepOrder();
                foreach (string name in parameterNamesInBindOrder)
                {
                    IValueProvider provider = _valueProviders[name];
                    IValueBinder binder = provider as IValueBinder;

                    if (binder != null)
                    {
                        bool isReturn = name == FunctionIndexer.ReturnParamName;
                        object argument = isReturn ? _returnValue : InvokeParameters[GetParameterIndex(name)];

                        try
                        {
                            // This could do complex things that may fail. Catch the exception.
                            await binder.SetValueAsync(argument, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            string message = string.Format(CultureInfo.InvariantCulture,
                                "Error while handling parameter {0} after function returned:", name);
                            throw new InvalidOperationException(message, exception);
                        }
                    }
                }
            }

            private int GetParameterIndex(string name)
            {
                var parameterNames = _functionInstance.Invoker.ParameterNames;
                for (int index = 0; index < parameterNames.Count; index++)
                {
                    if (parameterNames[index] == name)
                    {
                        return index;
                    }
                }

                throw new InvalidOperationException("Cannot find parameter + " + name + ".");
            }

            private string[] SortParameterNamesInStepOrder()
            {
                string[] parameterNames = _valueProviders.Keys.ToArray();

                if (parameterNames.Length > 1)
                {
                    IValueProvider[] parameterValues = _valueProviders.Values.ToArray();
                    Array.Sort(parameterValues, parameterNames, ValueBinderStepOrderComparer.Instance);
                }

                return parameterNames;
            }

            internal void SetReturnValue(object returnValue)
            {
                _returnValue = returnValue;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    if (_valueProviders != null)
                    {
                        foreach (var provider in _valueProviders.Values)
                        {
                            if (provider is IDisposable disposableItem)
                            {
                                disposableItem.Dispose();
                            }
                        }
                    }

                    if (JobInstance is IDisposable)
                    {
                        ((IDisposable)JobInstance).Dispose();
                    }

                    _disposed = true;
                }
            }

            private class ValueBinderStepOrderComparer : IComparer<IValueProvider>
            {
                private static readonly ValueBinderStepOrderComparer Singleton = new ValueBinderStepOrderComparer();

                private ValueBinderStepOrderComparer()
                {
                }

                public static ValueBinderStepOrderComparer Instance
                {
                    get
                    {
                        return Singleton;
                    }
                }

                public int Compare(IValueProvider x, IValueProvider y)
                {
                    return Comparer<int>.Default.Compare((int)GetStepOrder(x), (int)GetStepOrder(y));
                }

                private static BindStepOrder GetStepOrder(IValueProvider provider)
                {
                    IOrderedValueBinder orderedBinder = provider as IOrderedValueBinder;

                    if (orderedBinder == null)
                    {
                        return BindStepOrder.Default;
                    }

                    return orderedBinder.StepOrder;
                }
            }
        }

        private class FunctionInvocationFilterInvoker : IFunctionInvokerEx
        {
            private List<IFunctionInvocationFilter> _filters;
            private IFunctionInvokerEx _innerInvoker;
            private IFunctionInstance _functionInstance;
            private ParameterHelper _parameterHelper;
            private ILogger _logger;

            public IReadOnlyList<string> ParameterNames => _innerInvoker.ParameterNames;

            public static IFunctionInvokerEx Create(IFunctionInvokerEx innerInvoker, IEnumerable<IFunctionInvocationFilter> filters, IFunctionInstance functionInstance, ParameterHelper parameterHelper, ILogger logger)
            {
                if (!filters.Any())
                {
                    return innerInvoker;
                }

                return new FunctionInvocationFilterInvoker
                {
                    _innerInvoker = innerInvoker,
                    _filters = filters.ToList(),
                    _functionInstance = functionInstance,
                    _parameterHelper = parameterHelper,
                    _logger = logger
                };
            }

            public object CreateInstance()
            {
                throw new NotSupportedException($"{nameof(CreateInstance)} is not supported. Please use the overload that accepts an instance of an {nameof(IFunctionInstance)}");
            }

            public object CreateInstance(IFunctionInstanceEx functionInstance)
            {
                return _innerInvoker.CreateInstance(functionInstance);
            }

            public async Task<object> InvokeAsync(object instance, object[] arguments)
            {
                // Invoke the filter pipeline.
                // Basic rules:
                // - Iff a Pre filter runs, then run the corresponding post. 
                // - if a pre-filter fails, short circuit the pipeline. Skip the remaining pre-filters and body.

                CancellationToken cancellationToken;
                int len = _filters.Count;
                int highestSuccessfulFilter = -1;

                Exception exception = null;
                var properties = _parameterHelper.FilterContextProperties;
                FunctionExecutingContext executingContext = new FunctionExecutingContext(_parameterHelper.GetParametersAsDictionary(), properties, _functionInstance.Id, _functionInstance.FunctionDescriptor.ShortName, _logger);
                try
                {
                    for (int i = 0; i < len; i++)
                    {
                        var filter = _filters[i];

                        await filter.OnExecutingAsync(executingContext, cancellationToken);

                        highestSuccessfulFilter = i;
                    }

                    var result = await _innerInvoker.InvokeAsync(instance, arguments);
                    return result;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    throw;
                }
                finally
                {
                    var functionResult = exception != null ?
                        new FunctionResult(exception)
                        : new FunctionResult(true);
                    FunctionExecutedContext executedContext = new FunctionExecutedContext(_parameterHelper.GetParametersAsDictionary(), properties, _functionInstance.Id, _functionInstance.FunctionDescriptor.ShortName, _logger, functionResult);

                    // Run post filters in reverse order. 
                    // Only run the post if the corresponding pre executed successfully. 
                    for (int i = highestSuccessfulFilter; i >= 0; i--)
                    {
                        var filter = _filters[i];

                        try
                        {
                            await filter.OnExecutedAsync(executedContext, cancellationToken);
                        }
                        catch (Exception e)
                        {
                            exception = e;
                            executedContext.FunctionResult = new FunctionResult(exception);
                        }
                    }

                    // last exception wins. 
                    // If the body threw, then the finally will automatically rethrow that. 
                    // If a post-filter throws, capture that 
                    if (exception != null)
                    {
                        ExceptionDispatchInfo.Capture(exception).Throw();
                    }
                }
            }
        }
    }
}