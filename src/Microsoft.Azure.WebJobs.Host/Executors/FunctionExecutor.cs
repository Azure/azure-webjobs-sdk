﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    // In-memory executor. 
    class FunctionExecutor : IFunctionExecutor
    {
        private readonly FunctionExecutorContext _context;

        public FunctionExecutor(FunctionExecutorContext context)
        {
            _context = context;
        }

        public async Task<IDelayedException> TryExecuteAsync(IFunctionInstance instance,
            CancellationToken cancellationToken)
        {
            FunctionStartedMessage startedMessage = CreateStartedMessageWithoutArguments(instance);
            IDictionary<string, ParameterLog> parameterLogCollector = new Dictionary<string, ParameterLog>();
            FunctionCompletedMessage completedMessage = null;

            ExceptionDispatchInfo exceptionInfo = null;

            string startedMessageId = null;
            try
            {
                startedMessageId = await ExecuteWithLogMessageAsync(instance, startedMessage, parameterLogCollector,
                    cancellationToken);
                completedMessage = CreateCompletedMessage(startedMessage);
            }
            catch (Exception exception)
            {
                if (completedMessage == null)
                {
                    completedMessage = CreateCompletedMessage(startedMessage);
                }

                completedMessage.Failure = new FunctionFailure
                {
                    ExceptionType = exception.GetType().FullName,
                    ExceptionDetails = exception.ToDetails(),
                };

                exceptionInfo = ExceptionDispatchInfo.Capture(exception);
            }

            completedMessage.ParameterLogs = parameterLogCollector;
            completedMessage.EndTime = DateTimeOffset.UtcNow;

            bool loggedStartedEvent = startedMessageId != null;

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

            await _context.FunctionInstanceLogger.LogFunctionCompletedAsync(completedMessage,
                logCompletedCancellationToken);

            if (loggedStartedEvent)
            {
                await _context.FunctionInstanceLogger.DeleteLogFunctionStartedAsync(startedMessageId,
                    cancellationToken);
            }

            return exceptionInfo != null ? new ExceptionDispatchInfoDelayedException(exceptionInfo) : null;
        }

        private async Task<string> ExecuteWithLogMessageAsync(IFunctionInstance instance,
            FunctionStartedMessage message,
            IDictionary<string, ParameterLog> parameterLogCollector,
            CancellationToken cancellationToken)
        {
            string startedMessageId;

            // Create the console output writer
            IFunctionOutputDefinition outputDefinition = await _context.FunctionOutputLogger.CreateAsync(instance,
                cancellationToken);

            using (IFunctionOutput outputLog = await outputDefinition.CreateOutputAsync(cancellationToken))
            using (ITaskSeriesTimer updateOutputLogTimer = StartOutputTimer(outputLog.UpdateCommand))
            {
                TextWriter consoleOutput = outputLog.Output;
                FunctionBindingContext functionContext =
                    new FunctionBindingContext(_context.BindingContext, instance.Id, cancellationToken, consoleOutput);

                // Must bind before logging (bound invoke string is included in log message).
                IReadOnlyDictionary<string, IValueProvider> parameters =
                    await instance.BindingSource.BindAsync(new ValueBindingContext(functionContext, cancellationToken));

                using (ValueProviderDisposable.Create(parameters))
                {
                    startedMessageId = await LogFunctionStartedAsync(message, outputDefinition, parameters,
                        cancellationToken);

                    try
                    {
                        await ExecuteWithOutputLogsAsync(instance, parameters, consoleOutput, outputDefinition,
                            parameterLogCollector, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        consoleOutput.WriteLine("--------");
                        consoleOutput.WriteLine("Exception while executing:");
                        consoleOutput.Write(exception.ToDetails());
                        throw;
                    }
                }

                if (updateOutputLogTimer != null)
                {
                    await updateOutputLogTimer.StopAsync(cancellationToken);
                }

                await outputLog.SaveAndCloseAsync(cancellationToken);

                return startedMessageId;
            }
        }

        private Task<string> LogFunctionStartedAsync(FunctionStartedMessage message,
            IFunctionOutputDefinition functionOutput,
            IReadOnlyDictionary<string, IValueProvider> parameters,
            CancellationToken cancellationToken)
        {
            // Finish populating the function started snapshot.
            message.OutputBlob = functionOutput.OutputBlob;
            message.ParameterLogBlob = functionOutput.ParameterLogBlob;
            message.Arguments = CreateArguments(parameters);

            // Log that the function started.
            return _context.FunctionInstanceLogger.LogFunctionStartedAsync(message, cancellationToken);
        }

        private static ITaskSeriesTimer StartOutputTimer(IRecurrentCommand updateCommand)
        {
            if (updateCommand == null)
            {
                return null;
            }

            TimeSpan initialDelay = FunctionOutputIntervals.InitialDelay;
            TimeSpan refreshRate = FunctionOutputIntervals.RefreshRate;
            ITaskSeriesTimer timer = FixedDelayStrategy.CreateTimer(updateCommand, initialDelay, refreshRate);
            timer.Start();
            return timer;
        }

        private static ITaskSeriesTimer StartParameterLogTimer(IRecurrentCommand updateCommand)
        {
            if (updateCommand == null)
            {
                return null;
            }

            TimeSpan initialDelay = FunctionParameterLogIntervals.InitialDelay;
            TimeSpan refreshRate = FunctionParameterLogIntervals.RefreshRate;
            ITaskSeriesTimer timer = FixedDelayStrategy.CreateTimer(updateCommand, initialDelay, refreshRate);
            timer.Start();
            return timer;
        }

        private async Task ExecuteWithOutputLogsAsync(IFunctionInstance instance,
            IReadOnlyDictionary<string, IValueProvider> parameters,
            TextWriter consoleOutput,
            IFunctionOutputDefinition outputDefinition,
            IDictionary<string, ParameterLog> parameterLogCollector,
            CancellationToken cancellationToken)
        {
            IInvoker invoker = instance.Invoker;
            IReadOnlyDictionary<string, IWatcher> watches = CreateWatches(parameters);
            IRecurrentCommand updateParameterLogCommand =
                outputDefinition.CreateParameterLogUpdateCommand(watches, consoleOutput);

            using (ITaskSeriesTimer updateParameterLogTimer = StartParameterLogTimer(updateParameterLogCommand))
            {
                try
                {
                    await ExecuteWithWatchersAsync(invoker, parameters, consoleOutput, cancellationToken);

                    if (updateParameterLogTimer != null)
                    {
                        // Stop the watches after calling IValueBinder.SetValue (it may do things that should show up in
                        // the watches).
                        // Also, IValueBinder.SetValue could also take a long time (flushing large caches), and so it's
                        // useful to have watches still running.
                        await updateParameterLogTimer.StopAsync(cancellationToken);
                    }
                }
                finally
                {
                    ValueWatcher.AddLogs(watches, parameterLogCollector);
                }
            }
        }

        private static IReadOnlyDictionary<string, IWatcher> CreateWatches(
            IReadOnlyDictionary<string, IValueProvider> parameters)
        {
            Dictionary<string, IWatcher> watches = new Dictionary<string, IWatcher>();

            foreach (KeyValuePair<string, IValueProvider> item in parameters)
            {
                IWatchable watchable = item.Value as IWatchable;

                if (watchable != null)
                {
                    watches.Add(item.Key, watchable.Watcher);
                }
            }

            return watches;
        }

        private static ValueWatcher CreateValueWatcher(IReadOnlyDictionary<string, IWatcher> watches,
            CloudBlockBlob parameterLogBlob, TextWriter consoleOutput)
        {
            if (parameterLogBlob == null)
            {
                return null;
            }

            return new ValueWatcher(watches, parameterLogBlob, consoleOutput);
        }

        internal static async Task ExecuteWithWatchersAsync(IInvoker invoker,
            IReadOnlyDictionary<string, IValueProvider> parameters,
            TextWriter consoleOutput,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<string> parameterNames = invoker.ParameterNames;
            IDelayedException delayedBindingException;
            object[] invokeParameters = PrepareParameters(parameterNames, parameters, out delayedBindingException);

            if (delayedBindingException != null)
            {
                // This is done inside a watcher context so that each binding error is publish next to the binding in
                // the parameter status log.
                delayedBindingException.Throw();
            }

            Exception methodException = null;

            try
            {
                // Cancellation token is provide by invokeParameters (if the method binds to CancellationToken).
                await invoker.InvokeAsync(invokeParameters);
            }
            catch (TargetInvocationException exception)
            {
                // $$$ Beware, this loses the stack trace from the user's invocation
                // Print stacktrace to console now while we have it.
                consoleOutput.WriteLine(exception.InnerException.StackTrace);

                methodException = exception.InnerException;
            }

            // Process any out parameters, do any cleanup
            // For update, do any cleanup work.

            // Ensure IValueBinder.SetValue is called in BindOrder. This ordering is particularly important for
            // ensuring queue outputs occur last. That way, all other function side-effects are guaranteed to have
            // occurred by the time messages are enqueued.
            string[] parameterNamesInBindOrder = SortParameterNamesInStepOrder(parameters);

            foreach (string name in parameterNamesInBindOrder)
            {
                IValueProvider provider = parameters[name];
                IValueBinder binder = provider as IValueBinder;

                if (binder != null)
                {
                    object argument = invokeParameters[GetParameterIndex(parameterNames, name)];

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
                        string message = String.Format(CultureInfo.InvariantCulture,
                            "Error while handling parameter {0} '{1}' after function returned:", name, argument);
                        throw new InvalidOperationException(message, exception);
                    }
                }
            }

            if (methodException != null)
            {
                throw methodException;
            }
        }

        private static object[] PrepareParameters(IReadOnlyList<string> parameterNames,
            IReadOnlyDictionary<string, IValueProvider> parameters, out IDelayedException delayedBindingException)
        {
            object[] reflectionParameters = new object[parameterNames.Count];
            List<Exception> bindingExceptions = new List<Exception>();

            for (int index = 0; index < parameterNames.Count; index++)
            {
                string name = parameterNames[index];
                IValueProvider provider = parameters[name];

                BindingExceptionValueProvider exceptionProvider = provider as BindingExceptionValueProvider;

                if (exceptionProvider != null)
                {
                    bindingExceptions.Add(exceptionProvider.Exception);
                }

                reflectionParameters[index] = parameters[name].GetValue();
            }

            if (bindingExceptions.Count == 0)
            {
                delayedBindingException = null;
            }
            else if (bindingExceptions.Count == 1)
            {
                delayedBindingException = new DelayedException(bindingExceptions[0]);
            }
            else
            {
                delayedBindingException = new DelayedException(new AggregateException(bindingExceptions));
            }

            return reflectionParameters;
        }

        private FunctionStartedMessage CreateStartedMessageWithoutArguments(IFunctionInstance instance)
        {
            return new FunctionStartedMessage
            {
                HostInstanceId = _context.HostOutputMessage.HostInstanceId,
                HostDisplayName = _context.HostOutputMessage.HostDisplayName,
                SharedQueueName = _context.HostOutputMessage.SharedQueueName,
                InstanceQueueName = _context.HostOutputMessage.InstanceQueueName,
                Heartbeat = _context.HostOutputMessage.Heartbeat,
                WebJobRunIdentifier = _context.HostOutputMessage.WebJobRunIdentifier,
                FunctionInstanceId = instance.Id,
                Function = instance.FunctionDescriptor,
                ParentId = instance.ParentId,
                Reason = instance.Reason,
                StartTime = DateTimeOffset.UtcNow
            };
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
                Reason = startedMessage.Reason,
                StartTime = startedMessage.StartTime,
                OutputBlob = startedMessage.OutputBlob,
                ParameterLogBlob = startedMessage.ParameterLogBlob
            };
        }

        private static IDictionary<string, string> CreateArguments(IReadOnlyDictionary<string, IValueProvider> parameters)
        {
            IDictionary<string, string> arguments = new Dictionary<string, string>();

            if (parameters != null)
            {
                foreach (KeyValuePair<string, IValueProvider> parameter in parameters)
                {
                    arguments.Add(parameter.Key, parameter.Value.ToInvokeString());
                }
            }

            return arguments;
        }

        private static int GetParameterIndex(IReadOnlyList<string> parameterNames, string name)
        {
            for (int index = 0; index < parameterNames.Count; index++)
            {
                if (parameterNames[index] == name)
                {
                    return index;
                }
            }

            throw new InvalidOperationException("Cannot find parameter + " + name + ".");
        }

        private static string[] SortParameterNamesInStepOrder(IReadOnlyDictionary<string, IValueProvider> parameters)
        {
            string[] parameterNames = new string[parameters.Count];
            int index = 0;

            foreach (string parameterName in parameters.Keys)
            {
                parameterNames[index] = parameterName;
                index++;
            }

            IValueProvider[] parameterValues = new IValueProvider[parameters.Count];
            index = 0;

            foreach (IValueProvider parameterValue in parameters.Values)
            {
                parameterValues[index] = parameterValue;
                index++;
            }

            Array.Sort(parameterValues, parameterNames, ValueBinderStepOrderComparer.Instance);
            return parameterNames;
        }

        private static Task GetTaskFromReturnValue(MethodInfo method, object returnValue)
        {
            if (typeof(Task).IsAssignableFrom(method.ReturnType))
            {
                Task task = (Task)returnValue;

                if (task is Task<Task>)
                {
                    throw new InvalidOperationException("Returning a nested Task is not supported. " +
                        "Did you mean to await the task instead of returning it?");
                }

                return task;
            }

            return Task.FromResult(0);
        }

        private class ValueBinderStepOrderComparer : IComparer<IValueProvider>
        {
            private static readonly ValueBinderStepOrderComparer _instance = new ValueBinderStepOrderComparer();

            private ValueBinderStepOrderComparer()
            {
            }

            public static ValueBinderStepOrderComparer Instance { get { return _instance; } }

            public int Compare(IValueProvider x, IValueProvider y)
            {
                int xOrder = GetStepOrder(x);
                int yOrder = GetStepOrder(y);

                return Comparer<int>.Default.Compare(xOrder, yOrder);
            }

            private static int GetStepOrder(IValueProvider provider)
            {
                IOrderedValueBinder orderedBinder = provider as IOrderedValueBinder;

                if (orderedBinder == null)
                {
                    return BindStepOrders.Default;
                }

                return orderedBinder.StepOrder;
            }
        }
    }
}
