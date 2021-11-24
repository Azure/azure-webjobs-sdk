// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Timers
{
    /// <summary>Represents a timer that executes one task after another in a series.</summary>
    internal sealed class TaskSeriesTimer : ITaskSeriesTimer
    {
        private readonly ITaskSeriesCommand _command;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly Task _initialWait;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private bool _started;
        private bool _stopped;
        private Task _run;
        private bool _disposed;

        public TaskSeriesTimer(ITaskSeriesCommand command, IWebJobsExceptionHandler exceptionHandler,
            Task initialWait)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }

            if (exceptionHandler == null)
            {
                throw new ArgumentNullException("exceptionHandler");
            }

            if (initialWait == null)
            {
                throw new ArgumentNullException("initialWait");
            }

            _command = command;
            _exceptionHandler = exceptionHandler;
            _initialWait = initialWait;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            ThrowIfDisposed();

            if (_started)
            {
                throw new InvalidOperationException("The timer has already been started; it cannot be restarted.");
            }

            _run = RunAsync(_cancellationTokenSource.Token);
            _started = true;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (!_started)
            {
                throw new InvalidOperationException("The timer has not yet been started.");
            }

            if (_stopped)
            {
                throw new InvalidOperationException("The timer has already been stopped.");
            }

            _cancellationTokenSource.Cancel();
            return StopAsyncCore(cancellationToken);
        }

        private async Task StopAsyncCore(CancellationToken cancellationToken)
        {
            if (!_run.IsCompleted)
            {
#if NET6_0_OR_GREATER
                await _run.WaitAsync(cancellationToken);
#else
                TaskCompletionSource<object> cancellationTaskSource = new TaskCompletionSource<object>();

                using (_cancellationTokenSource.Token.Register((cts) => ((TaskCompletionSource<object>)cts).SetCanceled(), cancellationTaskSource))
                {
                    // Wait for all pending command tasks to complete (or cancellation of the token) before returning.
                    await Task.WhenAny(_run, cancellationTaskSource.Task);
                }
#endif
            }

            _stopped = true;
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            _cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Running callers might still be using the cancellation token.
                // Mark it canceled but don't dispose of the source while the callers are running.
                // Otherwise, callers would receive ObjectDisposedException when calling token.Register.
                // For now, rely on finalization to clean up _cancellationTokenSource's wait handle (if allocated).
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();

                _disposed = true;
            }
        }

        private async Task RunAsync()
        {
            try
            {
                // Allow Start to return immediately without waiting for any initial iteration work to start.
                await Task.Yield();

                Task wait = _initialWait;

#if !NET6_0_OR_GREATER
                TaskCompletionSource<object> cancellationTaskSource = new TaskCompletionSource<object>();
                using var _ = _cancellationTokenSource.Token.Register((cts) => ((TaskCompletionSource<object>)cts).SetCanceled(), cancellationTaskSource);
#endif
                // Execute tasks one at a time (in a series) until stopped.
                // TODO: Remove spacing, minimizing diff
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                        try
                        {
#if NET6_0_OR_GREATER
                            await wait.WaitAsync(_cancellationTokenSource.Token);
#else
                            await Task.WhenAny(wait, cancellationTaskSource.Task);
#endif
                        }
                        catch (OperationCanceledException)
                        {
                            // When Stop fires, don't make it wait for wait before it can return.
                        }

                    if (_cancellationTokenSource.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        TaskSeriesCommandResult result = await _command.ExecuteAsync(_cancellationTokenSource.Token);
                        wait = result.Wait;
                    }
                    catch (Exception ex) when (ex.InnerException is OperationCanceledException)
                    {
                        // OperationCanceledExceptions coming from storage are wrapped in a StorageException.
                        // We'll handle them all here so they don't have to be managed for every call.
                    }
                    catch (OperationCanceledException)
                    {
                        // Don't fail the task, throw a background exception, or stop looping when a task cancels.
                    }
                }
            }
            catch (Exception exception)
            {
                // Immediately report any unhandled exception from this background task.
                // (Don't capture the exception as a fault of this Task; that would delay any exception reporting until
                // Stop is called, which might never happen.)
                _exceptionHandler.OnUnhandledExceptionAsync(ExceptionDispatchInfo.Capture(exception)).GetAwaiter().GetResult();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }
        }
    }
}
