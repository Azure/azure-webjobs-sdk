// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class FunctionExecutor : IFunctionExecutor
    {
        private readonly IFunctionInstanceLogger _functionInstanceLogger;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly IAsyncCollector<FunctionInstanceLogEntry> _functionEventCollector;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _resultsLogger;
        private readonly IEnumerable<IFunctionFilter> _globalFunctionFilters;

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
                IServiceScopeFactory serviceScopeFactory,
                ILoggerFactory loggerFactory = null,
                IEnumerable<IFunctionFilter> globalFunctionFilters = null)
        {
            _functionInstanceLogger = functionInstanceLogger ?? throw new ArgumentNullException(nameof(functionInstanceLogger));
            _functionOutputLogger = functionOutputLogger;
            _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
            _functionEventCollector = functionEventCollector ?? throw new ArgumentNullException(nameof(functionEventCollector));
            _serviceScopeFactory = serviceScopeFactory;
            _loggerFactory = loggerFactory;
            _resultsLogger = _loggerFactory?.CreateLogger(LogCategories.Results);
            _globalFunctionFilters = globalFunctionFilters ?? Enumerable.Empty<IFunctionFilter>();
        }

        public HostOutputMessage HostOutputMessage
        {
            get { return _hostOutputMessage; }
            set { _hostOutputMessage = value; }
        }

        public async Task<IDelayedException> TryExecuteAsync(IFunctionInstance instance, CancellationToken cancellationToken)
        {
            bool ownsInstance = false;

            if (!(instance is IFunctionInstanceEx functionInstance))
            {
                functionInstance = new FunctionInstanceWrapper(instance, _serviceScopeFactory);
                ownsInstance = true;
            }

            try
            {
                return await TryExecuteAsyncCore(functionInstance, cancellationToken);
            }
            finally
            {
                if (ownsInstance)
                {
                    (functionInstance as IDisposable)?.Dispose();
                }
            }
        }

        private async Task<IDelayedException> TryExecuteAsyncCore(IFunctionInstanceEx functionInstance, CancellationToken cancellationToken)
        {

            ILogger logger = _loggerFactory?.CreateLogger(LogCategories.CreateFunctionCategory(functionInstance.FunctionDescriptor.LogName));

            FunctionStartedMessage functionStartedMessage = CreateStartedMessageWithoutArguments(functionInstance);
            var parameterHelper = new ParameterHelper(functionInstance);
            FunctionCompletedMessage functionCompletedMessage = null;
            ExceptionDispatchInfo exceptionInfo = null;
            string functionStartedMessageId = null;
            FunctionInstanceLogEntry instanceLogEntry = null;

            using (_resultsLogger?.BeginFunctionScope(functionInstance, HostOutputMessage.HostInstanceId))
            using (parameterHelper)
            {
                try
                {
                    instanceLogEntry = await NotifyPreBindAsync(functionStartedMessage);
                    parameterHelper.Initialize();
                    functionStartedMessageId = await ExecuteWithLoggingAsync(functionInstance, functionStartedMessage, instanceLogEntry, parameterHelper, logger, cancellationToken);
                    functionCompletedMessage = CreateCompletedMessage(functionStartedMessage);
                }
                catch (Exception exception)
                {
                    if (functionCompletedMessage == null)
                    {
                        functionCompletedMessage = CreateCompletedMessage(functionStartedMessage);
                    }

                    functionCompletedMessage.Failure = new FunctionFailure
                    {
                        Exception = exception,
                        ExceptionType = exception.GetType().FullName,
                        ExceptionDetails = exception.ToDetails(),
                    };

                    exceptionInfo = ExceptionDispatchInfo.Capture(exception);
                    exceptionInfo = await InvokeExceptionFiltersAsync(parameterHelper.JobInstance, exceptionInfo, functionInstance, parameterHelper.FilterContextProperties, logger, cancellationToken);
                }

                if (functionCompletedMessage != null)
                {
                    functionCompletedMessage.ParameterLogs = parameterHelper.ParameterLogCollector;
                    functionCompletedMessage.EndTime = DateTimeOffset.UtcNow;
                }

                bool loggedStartedEvent = functionStartedMessageId != null;
                CancellationToken logCompletedCancellationToken;
                if (loggedStartedEvent)
                {
                    // If function started was logged, don't cancel calls to log function completed.
                    logCompletedCancellationToken = CancellationToken.None;
                }
                else
                {
                    logCompletedCancellationToken = cancellationToken;
                }

                if (functionCompletedMessage != null)
                {
                    await _functionInstanceLogger.LogFunctionCompletedAsync(functionCompletedMessage, logCompletedCancellationToken);
                }

                if (instanceLogEntry != null)
                {
                    await NotifyCompleteAsync(instanceLogEntry, functionCompletedMessage.Arguments, exceptionInfo);
                    _resultsLogger?.LogFunctionResult(instanceLogEntry);
                }

                if (loggedStartedEvent)
                {
                    await _functionInstanceLogger.DeleteLogFunctionStartedAsync(functionStartedMessageId, cancellationToken);
                }
            }

            if (exceptionInfo != null)
            {
                await HandleExceptionAsync(functionInstance.FunctionDescriptor.TimeoutAttribute, exceptionInfo, _exceptionHandler);
            }

            return exceptionInfo != null ? new ExceptionDispatchInfoDelayedException(exceptionInfo) : null;
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

        private async Task<string> ExecuteWithLoggingAsync(IFunctionInstanceEx instance, FunctionStartedMessage message,
            FunctionInstanceLogEntry instanceLogEntry, ParameterHelper parameterHelper, ILogger logger, CancellationToken cancellationToken)
        {
            IFunctionOutputDefinition outputDefinition = null;
            IFunctionOutput outputLog = null;
            ITaskSeriesTimer updateOutputLogTimer = null;
            TextWriter functionOutputTextWriter = null;

            IFunctionOutputLogger outputLogger = _functionOutputLogger;
            outputDefinition = await outputLogger.CreateAsync(instance, cancellationToken);
            outputLog = outputDefinition.CreateOutput();
            functionOutputTextWriter = outputLog.Output;
            updateOutputLogTimer = StartOutputTimer(outputLog.UpdateCommand, _exceptionHandler);

            try
            {
                // Create a linked token source that will allow us to signal function cancellation
                // (e.g. Based on TimeoutAttribute, etc.)                
                CancellationTokenSource functionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                using (functionCancellationTokenSource)
                {
                    // This allows the FunctionOutputLogger to grab the TextWriter created by the IFunctionOutput. This enables user ILogger
                    // output to be forwarded to the dashboard logs.
                    // TODO: Refactor all of this logging to be implemented as a separate ILogger.
                    FunctionOutputLogger.SetOutput(outputLog);

                    // Must bind before logging (bound invoke string is included in log message).
                    var functionContext = new FunctionBindingContext(
                        instance.Id,
                        functionCancellationTokenSource.Token,
                        instance.InstanceServices,
                        instance.FunctionDescriptor);
                    var valueBindingContext = new ValueBindingContext(functionContext, cancellationToken);

                    using (logger.BeginScope(_inputBindingScope))
                    {
                        await parameterHelper.BindAsync(instance.BindingSource, valueBindingContext);
                    }

                    Exception invocationException = null;
                    ExceptionDispatchInfo exceptionInfo = null;
                    string startedMessageId = null;
                    using (parameterHelper)
                    {
                        startedMessageId = await LogFunctionStartedAsync(message, outputDefinition, parameterHelper, cancellationToken);

                        // Log started
                        await NotifyPostBindAsync(instanceLogEntry, message.Arguments);

                        try
                        {
                            await ExecuteWithLoggingAsync(instance, parameterHelper, outputDefinition, logger, functionCancellationTokenSource);
                        }
                        catch (Exception ex)
                        {
                            invocationException = ex;
                        }
                    }

                    if (invocationException != null)
                    {
                        // In the event of cancellation or timeout, we use the original exception without additional logging.
                        if (invocationException is OperationCanceledException || invocationException is FunctionTimeoutException)
                        {
                            exceptionInfo = ExceptionDispatchInfo.Capture(invocationException);
                        }
                        else
                        {
                            string errorMessage = string.Format("Exception while executing function: {0}", instance.FunctionDescriptor.ShortName);
                            FunctionInvocationException fex = new FunctionInvocationException(errorMessage, instance.Id, instance.FunctionDescriptor.FullName, invocationException);
                            exceptionInfo = ExceptionDispatchInfo.Capture(fex);
                        }
                    }

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
                        // release any held singleton lock immediately
                        SingletonLock singleton = await parameterHelper.GetSingletonLockAsync();
                        if (singleton != null && singleton.IsHeld)
                        {
                            await singleton.ReleaseAsync(cancellationToken);
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
                OnFunctionTimeout(timer, instance.FunctionDescriptor, instance.Id, timeout, attribute.TimeoutWhileDebugging, logger, cancellationTokenSource,
                    () => Debugger.IsAttached);
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

        private Task<string> LogFunctionStartedAsync(FunctionStartedMessage message,
            IFunctionOutputDefinition functionOutput,
            ParameterHelper parameterHelper,
            CancellationToken cancellationToken)
        {
            // Finish populating the function started snapshot.
            message.OutputBlob = functionOutput.OutputBlob;
            message.ParameterLogBlob = functionOutput.ParameterLogBlob;
            message.Arguments = parameterHelper.CreateInvokeStringArguments();

            // Log that the function started.
            return _functionInstanceLogger.LogFunctionStartedAsync(message, cancellationToken);
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

        private async Task ExecuteWithLoggingAsync(IFunctionInstanceEx instance,
            ParameterHelper parameterHelper,
            IFunctionOutputDefinition outputDefinition,
            ILogger logger,
            CancellationTokenSource functionCancellationTokenSource)
        {
            IFunctionInvoker invoker = instance.Invoker;

            ITaskSeriesTimer updateParameterLogTimer = null;

            var parameterWatchers = parameterHelper.CreateParameterWatchers();
            IRecurrentCommand updateParameterLogCommand = outputDefinition.CreateParameterLogUpdateCommand(parameterWatchers, logger);
            updateParameterLogTimer = StartParameterLogTimer(updateParameterLogCommand, _exceptionHandler);

            try
            {
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
            finally
            {
                if (updateParameterLogTimer != null)
                {
                    updateParameterLogTimer.Dispose();
                }

                parameterHelper.FlushParameterWatchers();
            }
        }

        internal async Task ExecuteWithWatchersAsync(IFunctionInstanceEx instance,
            ParameterHelper parameterHelper,
            ILogger logger,
            CancellationTokenSource functionCancellationTokenSource)
        {
            IFunctionInvokerEx invoker = instance.GetFunctionInvoker();
            IDelayedException delayedBindingException = await parameterHelper.PrepareParametersAsync();

            if (delayedBindingException != null)
            {
                // This is done inside a watcher context so that each binding error is publish next to the binding in
                // the parameter status log.
                delayedBindingException.Throw();
            }

            // if the function is a Singleton, aquire the lock
            SingletonLock singleton = await parameterHelper.GetSingletonLockAsync();
            if (singleton != null)
            {
                await singleton.AcquireAsync(functionCancellationTokenSource.Token);
            }

            object jobInstance = parameterHelper.JobInstance;
            using (CancellationTokenSource timeoutTokenSource = new CancellationTokenSource())
            {
                TimeoutAttribute timeoutAttribute = instance.FunctionDescriptor.TimeoutAttribute;
                bool throwOnTimeout = timeoutAttribute == null ? false : timeoutAttribute.ThrowOnTimeout;
                var timer = StartFunctionTimeout(instance, timeoutAttribute, timeoutTokenSource, logger);
                TimeSpan timerInterval = timer == null ? TimeSpan.MinValue : TimeSpan.FromMilliseconds(timer.Interval);
                try
                {
                    var filters = GetFilters<IFunctionInvocationFilter>(_globalFunctionFilters, instance.FunctionDescriptor, jobInstance);

                    invoker = FunctionInvocationFilterInvoker.Create(invoker, filters, instance, parameterHelper, logger);


                    using (logger.BeginScope(new Dictionary<string, object>
                    {
                        [LogConstants.CategoryNameKey] = LogCategories.CreateFunctionCategory(instance.FunctionDescriptor.LogName),
                        [LogConstants.LogLevelKey] = LogLevel.Information
                    }))
                    {
                        await InvokeAsync(invoker, parameterHelper, timeoutTokenSource, functionCancellationTokenSource,
                            throwOnTimeout, timerInterval, instance);
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

        internal static async Task InvokeAsync(IFunctionInvoker invoker, ParameterHelper parameterHelper, CancellationTokenSource timeoutTokenSource,
            CancellationTokenSource functionCancellationTokenSource, bool throwOnTimeout, TimeSpan timerInterval, IFunctionInstance instance)
        {
            object[] invokeParameters = parameterHelper.InvokeParameters;

            // There are three ways the function can complete:
            //   1. The invokeTask itself completes first.
            //   2. A cancellation is requested (by host.Stop(), for example).
            //      a. Continue waiting for the invokeTask to complete. Either #1 or #3 will occur.
            //   3. A timeout fires.
            //      a. If throwOnTimeout, we throw the FunctionTimeoutException.
            //      b. If !throwOnTimeout, wait for the task to complete.

            // Start the invokeTask.
            Task<object> invokeTask = invoker.InvokeAsync(parameterHelper.JobInstance, invokeParameters);

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

            object returnValue = await invokeTask;

            parameterHelper.SetReturnValue(returnValue);
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
        private static List<TFilter> GetFilters<TFilter>(IEnumerable<IFunctionFilter> globalFunctionFilters, FunctionDescriptor functionDescriptor, object instance) where TFilter : class, IFunctionFilter
        {
            var filters = new List<TFilter>();

            // If the job method is an instance method and the job class implements
            // the filter interface, this filter runs first before all other filters
            TFilter instanceFilter = instance as TFilter;
            if (instanceFilter != null)
            {
                filters.Add(instanceFilter);
            }

            // Add any global filters
            filters.AddRange(globalFunctionFilters.OfType<TFilter>());

            // Next, any class level filters are added
            if (functionDescriptor.ClassLevelFilters != null)
            {
                filters.AddRange(functionDescriptor.ClassLevelFilters.OfType<TFilter>());
            }

            // Finally, any method level filters are added
            if (functionDescriptor.MethodLevelFilters != null)
            {
                filters.AddRange(functionDescriptor.MethodLevelFilters.OfType<TFilter>());
            }

            return filters;
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

        private FunctionStartedMessage CreateStartedMessageWithoutArguments(IFunctionInstance instance)
        {
            FunctionStartedMessage message = new FunctionStartedMessage
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

        private static FunctionCompletedMessage CreateCompletedMessage(FunctionStartedMessage startedMessage)
        {
            return new FunctionCompletedMessage
            {
                HostInstanceId = startedMessage.HostInstanceId,
                HostDisplayName = startedMessage.HostDisplayName,
                SharedQueueName = startedMessage.SharedQueueName,
                InstanceQueueName = startedMessage.InstanceQueueName,
                Heartbeat = startedMessage.Heartbeat,
                WebJobRunIdentifier = startedMessage.WebJobRunIdentifier,
                FunctionInstanceId = startedMessage.FunctionInstanceId,
                Function = startedMessage.Function,
                Arguments = startedMessage.Arguments,
                ParentId = startedMessage.ParentId,
                TriggerDetails = startedMessage.TriggerDetails,
                Reason = startedMessage.Reason,
                ReasonDetails = startedMessage.FormatReason(),
                StartTime = startedMessage.StartTime,
                OutputBlob = startedMessage.OutputBlob,
                ParameterLogBlob = startedMessage.ParameterLogBlob
            };
        }

        // Called very early when function is started; before arguments are bound. 
        private async Task<FunctionInstanceLogEntry> NotifyPreBindAsync(FunctionStartedMessage functionStartedMessage)
        {
            FunctionInstanceLogEntry fastItem = new FunctionInstanceLogEntry
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
            Debug.Assert(fastItem.IsStart);

            // Log pre-bind event. 
            await _functionEventCollector.AddAsync(fastItem);

            return fastItem;
        }

        // Called before function body is executed; after arguments are bound. 
        private Task NotifyPostBindAsync(FunctionInstanceLogEntry fastItem, IDictionary<string, string> arguments)
        {
            // Log post-bind event. 
            fastItem.Arguments = arguments;
            Debug.Assert(fastItem.IsPostBind);

            return _functionEventCollector.AddAsync(fastItem);
        }

        // Called after function completes. 
        private Task NotifyCompleteAsync(FunctionInstanceLogEntry instanceLogEntry, IDictionary<string, string> arguments, ExceptionDispatchInfo exceptionInfo)
        {
            if (instanceLogEntry == null)
            {
                throw new ArgumentNullException(nameof(instanceLogEntry));
            }

            instanceLogEntry.LiveTimer.Stop();


            // log result            
            instanceLogEntry.EndTime = DateTime.UtcNow;
            instanceLogEntry.Duration = instanceLogEntry.LiveTimer.Elapsed;
            instanceLogEntry.Arguments = arguments;

            Debug.Assert(instanceLogEntry.IsCompleted);

            // Log completed
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

            return _functionEventCollector.AddAsync(instanceLogEntry);
        }

#region class ParameterHelper

        // Handle various phases of parameter building and logging.
        // The paramerter phases are: 
        //  1. Initial binding data from the trigger. 
        //  2. IValueProvider[]. Provides a System.Object along with additional information (liking logging) 
        //  3. System.Object[]. which can be passed to the actual MethodInfo for execution 
        internal class ParameterHelper : IDisposable
        {
            private class ParameterInfo
            {
                public string Name { get; private set; }
                public IValueProvider ValueProvider { get; private set; }
                public int InvocationIndex { get; private set; }
                public int InputBindingIndex { get; internal set; }
                public int OutputBindingIndex { get; internal set; }

                public ParameterInfo(string name, IValueProvider valueProvider, int invocationIndex)
                {
                    Name = name;
                    ValueProvider = valueProvider;
                    InvocationIndex = invocationIndex;
                    InputBindingIndex = 0;
                    OutputBindingIndex = 0;
                }
            }

            private readonly IFunctionInstanceEx _functionInstance;

            // Logs, contain the result from invoking the IWatchers.
            private IDictionary<string, ParameterLog> _parameterLogCollector = new Dictionary<string, ParameterLog>();

            // Optional runtime watchers for the parameters. 
            private IReadOnlyDictionary<string, IWatcher> _parameterWatchers;

            // ValueProviders for the parameters. These are produced from binding. 
            // This includes a possible $return for the return value. 
            private IReadOnlyDictionary<string, ParameterInfo> _parameters;

            // ordered parameter names of the underlying physical MethodInfo that will be invoked. 
            // This litererally matches the ParameterInfo[] and does not include return value. 
            private IReadOnlyList<string> _parameterNames;

            // state bag passed to all function filters
            private readonly IDictionary<string, object> _filterContextProperties = new Dictionary<string, object>();

            // the return value of the function
            private object _returnValue;

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
                _parameterNames = functionInstance.Invoker.ParameterNames;
            }

            // Phsyical objects to pass to the underlying method Info. These will get updated for out-parameters. 
            // These are produced by executing the binders. 
            public object[] InvokeParameters { get; internal set; }

            public object JobInstance { get; set; }

            public IDictionary<string, ParameterLog> ParameterLogCollector => _parameterLogCollector;

            public object ReturnValue => _returnValue;

            public IDictionary<string, object> FilterContextProperties => _filterContextProperties;

            public void Initialize()
            {
                JobInstance = _functionInstance.GetFunctionInvoker().CreateInstance(_functionInstance);
            }

            // Convert the parameters and their names to a dictionary
            public Dictionary<string, object> GetParametersAsDictionary()
            {
                Dictionary<string, object> parametersAsDictionary = new Dictionary<string, object>();

                int counter = 0;
                foreach (var name in _parameterNames)
                {
                    parametersAsDictionary[name] = InvokeParameters[counter];
                    counter++;
                }

                return parametersAsDictionary;
            }

            public IReadOnlyDictionary<string, IWatcher> CreateParameterWatchers()
            {
                if (_parameterWatchers != null)
                {
                    return _parameterWatchers;
                }
                Dictionary<string, IWatcher> watches = new Dictionary<string, IWatcher>();

                foreach (KeyValuePair<string, ParameterInfo> item in _parameters)
                {
                    IWatchable watchable = item.Value.ValueProvider as IWatchable;
                    if (watchable != null)
                    {
                        watches.Add(item.Key, watchable.Watcher);
                    }
                }

                _parameterWatchers = watches;
                return watches;
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

                    _parameterLogCollector.Add(item.Key, status);
                }
            }

            // The binding source has the set of trigger data and raw parameter binders. 
            // run the binding source to create a set of IValueProviders for this instance. 
            public async Task BindAsync(IBindingSource bindingSource, ValueBindingContext context)
            {
                IReadOnlyDictionary<string, IValueProvider> namedParameterBinders = await bindingSource.BindAsync(context);
                IReadOnlyDictionary<string, ParameterInfo> parameters = ComputeParameterOrder(namedParameterBinders);
                _parameters = parameters;
            }

            public IDictionary<string, string> CreateInvokeStringArguments()
            {
                IDictionary<string, string> arguments = new Dictionary<string, string>();

                if (_parameters != null)
                {
                    foreach (KeyValuePair<string, ParameterInfo> parameter in _parameters)
                    {
                        arguments.Add(parameter.Key, parameter.Value?.ValueProvider?.ToInvokeString() ?? "null");
                    }
                }

                return arguments;
            }

            // Run the IValueProviders to create the real set of underlying objects that we'll pass to the MethodInfo. 
            public async Task<IDelayedException> PrepareParametersAsync()
            {
                object[] reflectionParameters = new object[_parameterNames.Count];
                List<Exception> bindingExceptions = new List<Exception>();

                IEnumerable<ParameterInfo> parametersInBindOrder = _parameters.Values.OrderBy( (p) => p.InputBindingIndex );
                foreach (ParameterInfo parameter in parametersInBindOrder)
                {
                    BindingExceptionValueProvider exceptionProvider = parameter.ValueProvider as BindingExceptionValueProvider;
                    if (exceptionProvider != null)
                    {
                        bindingExceptions.Add(exceptionProvider.Exception);
                    }

                    object parameterValue = await parameter.ValueProvider.GetValueAsync();
                    if (parameter.InvocationIndex >= 0)
                    {
                        reflectionParameters[parameter.InvocationIndex] = parameterValue;
                    }
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

                InvokeParameters = reflectionParameters;
                return delayedBindingException;
            }

            // Retrieve the function singleton lock from the parameters. 
            // Null if not found. 
            public async Task<SingletonLock> GetSingletonLockAsync()
            {
                SingletonLock singleton = null;
                if (_parameters.TryGetValue(SingletonValueProvider.SingletonParameterName, out ParameterInfo singletonValueProvider))
                {
                    singleton = (SingletonLock)(await singletonValueProvider.ValueProvider.GetValueAsync());
                }

                return singleton;
            }

            // Process any out parameters and persist any pending values.
            // Ensure IValueBinder.SetValue is called in BindStepOrder. This ordering is particularly important for
            // ensuring queue outputs occur last. That way, all other function side-effects are guaranteed to have
            // occurred by the time messages are enqueued.
            public async Task ProcessOutputParameters(CancellationToken cancellationToken)
            {
                IEnumerable<ParameterInfo> parametersInBindOrder = _parameters.Values.OrderBy( (p) => p.OutputBindingIndex );
                foreach (ParameterInfo parameter in parametersInBindOrder)
                {
                    IValueBinder binder = parameter.ValueProvider as IValueBinder;

                    if (binder != null)
                    {
                        bool isReturn = FunctionIndexer.ReturnParamName.Equals(parameter.Name);
                        object argument = isReturn ? _returnValue : InvokeParameters[parameter.InvocationIndex];

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
                                "Error while handling parameter {0} after function returned:", parameter.Name);
                            throw new InvalidOperationException(message, exception);
                        }
                    }
                }
            }

            /// <summary>
            /// After executing bindingSource.BindAsync(context) we get a set of value providers that create/consume values
            /// before and after the function execution respectively.
            /// We need to make sure that the value binding is executed in the order described in <see cref="IOrderedValueBinder" />.
            /// This method computes the respective indices.
            /// </summary>
            /// <param name="namedParameterBinders">A map of parameter names to value providers.</param>
            /// <returns>A map of parameter names to parameter descriptors that encapsulate the value provider and the executuion indices.</returns>
            private IReadOnlyDictionary<string, ParameterInfo> ComputeParameterOrder(IReadOnlyDictionary<string, IValueProvider> namedParameterBinders)
            {
                int GetParameterInvocationIndex(string name)
                {
                    // Determining all param indices in this was is slow (n-square), but we expect the number or params to be not too large, so this is OK. 
                    for (int index = 0; index < _parameterNames.Count; index++)
                    {
                        if (_parameterNames[index].Equals(name))
                        {
                            return index;
                        }
                    }

                    return -1;
                }

                BindStepOrder GetBindStepOrderBucket(IValueProvider provider)
                {
                    IOrderedValueBinder orderedBinder = provider as IOrderedValueBinder;
                    if (orderedBinder == null)
                    {
                        return BindStepOrder.Default;
                    }

                    switch (orderedBinder.StepOrder)
                    {
                        case BindStepOrder.TightFunctionWrapper:
                        case BindStepOrder.Enqueue:
                        case BindStepOrder.Default:
                            return orderedBinder.StepOrder;

                        default:
                            return BindStepOrder.Default;
                    }
                }

                void InsertIntoBindStepOrderBucket(ParameterInfo parameter, List<ParameterInfo> bindStepOrderBucket)
                {
                    // Insert the current parameter into the bucket of parameters with the same BindStepOrder-kind;
                    // While doing so, order first by invocation index (parameter position), then by binding resolution order:
                    int i = 0;
                    while (i < bindStepOrderBucket.Count && parameter.InvocationIndex <= bindStepOrderBucket[i].InvocationIndex)
                    {
                        i++;
                    }

                    bindStepOrderBucket.Insert(i, parameter);
                }

                var parameters = new Dictionary<string, ParameterInfo>(capacity: namedParameterBinders.Count);

                var bindStepOrderBucket_TightFunctionWrapper = new List<ParameterInfo>();
                var bindStepOrderBucket_Enqueue = new List<ParameterInfo>();
                var bindStepOrderBucket_Default = new List<ParameterInfo>();
                var bindStepOrderBucket_EnqueueOrDefault = new List<ParameterInfo>();

                // Look at each bound parameter:
                foreach (KeyValuePair<string, IValueProvider> namedParameterBinder in namedParameterBinders)
                {
                    // Find the invocation index and the BindStepOrder-kind:
                    int invocationIndex = GetParameterInvocationIndex(namedParameterBinder.Key);
                    BindStepOrder bindStepOrderBucketKind = GetBindStepOrderBucket(namedParameterBinder.Value);

                    var parameter = new ParameterInfo(namedParameterBinder.Key, namedParameterBinder.Value, invocationIndex);
                    parameters.Add(namedParameterBinder.Key, parameter);

                    switch (bindStepOrderBucketKind)
                    {
                        case BindStepOrder.TightFunctionWrapper:
                        {
                            InsertIntoBindStepOrderBucket(parameter, bindStepOrderBucket_TightFunctionWrapper);
                            break;
                        }

                        case BindStepOrder.Enqueue:
                        {
                            InsertIntoBindStepOrderBucket(parameter, bindStepOrderBucket_Enqueue);
                            InsertIntoBindStepOrderBucket(parameter, bindStepOrderBucket_EnqueueOrDefault);
                            break;
                        }

                        case BindStepOrder.Default:
                        {
                            InsertIntoBindStepOrderBucket(parameter, bindStepOrderBucket_Default);
                            InsertIntoBindStepOrderBucket(parameter, bindStepOrderBucket_EnqueueOrDefault);
                            break;
                        }
                    }
                }

                {
                    // The input is bound in the order: (Enqueue | Default)-bucket >> (TightFunctionWrapper)-bucket
                    // Go through the buckets and determine the input binding order:

                    int inputBindingIndex = 0;

                    foreach (ParameterInfo parameter in bindStepOrderBucket_EnqueueOrDefault)
                    {
                        parameter.InputBindingIndex = inputBindingIndex++;
                    }

                    foreach (ParameterInfo parameter in bindStepOrderBucket_TightFunctionWrapper)
                    {
                        parameter.InputBindingIndex = inputBindingIndex++;
                    }
                }

                {
                    // The output is bound in the order: (TightFunctionWrapper)-bucket >> (Default)-bucket >> (Enqueue)-bucket
                    // Go through the buckets and determine the output binding order:

                    int outputBindingIndex = 0;

                    foreach (ParameterInfo parameter in bindStepOrderBucket_TightFunctionWrapper)
                    {
                        parameter.OutputBindingIndex = outputBindingIndex++;
                    }

                    foreach (ParameterInfo parameter in bindStepOrderBucket_Default)
                    {
                        parameter.OutputBindingIndex = outputBindingIndex++;
                    }

                    foreach (ParameterInfo parameter in bindStepOrderBucket_Enqueue)
                    {
                        parameter.OutputBindingIndex = outputBindingIndex++;
                    }
                }

                return parameters;
            }

            internal void SetReturnValue(object returnValue)
            {
                _returnValue = returnValue;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    if (_parameters != null)
                    {
                        foreach (var disposableItem in _parameters.Values.Select( (p) => p.ValueProvider ).OfType<IDisposable>())
                        {
                            disposableItem.Dispose();
                        }
                    }

                    if (JobInstance is IDisposable)
                    {
                        ((IDisposable)JobInstance).Dispose();
                    }

                    _disposed = true;
                }
            }
        }

#endregion class ParameterHelper

#region class ParameterHelper

        private class FunctionInvocationFilterInvoker : IFunctionInvokerEx
        {
            private List<IFunctionInvocationFilter> _filters;
            private IFunctionInvokerEx _innerInvoker;
            private IFunctionInstance _functionInstance;
            private ParameterHelper _parameterHelper;
            private ILogger _logger;

            public IReadOnlyList<string> ParameterNames => _innerInvoker.ParameterNames;

            public static IFunctionInvokerEx Create(IFunctionInvokerEx innerInvoker, List<IFunctionInvocationFilter> filters, IFunctionInstance functionInstance, ParameterHelper parameterHelper, ILogger logger)
            {
                if (filters.Count == 0)
                {
                    return innerInvoker;
                }

                return new FunctionInvocationFilterInvoker
                {
                    _innerInvoker = innerInvoker,
                    _filters = filters,
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

#endregion class ParameterHelper
    
    }
}
