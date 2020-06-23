// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// The class that implements a function execution with retries.
    /// </summary>
    internal class DefaultRetryManager : IRetryManager
    {
        private readonly ILogger _logger;
        private readonly string _id;
        private RetryAttribute _initialRetryPolicy;
        private const string RetryCountString = "retryCount";
        private const string RetryAttribute = "retryAttribute";

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="retryAttribute">Attribute that can be applied at the class or function level to set executions retries of job functions.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to create an <see cref="ILogger"/> from.</param>
        public DefaultRetryManager(RetryAttribute retryAttribute, ILoggerFactory loggerFactory)
        {
            _id = Guid.NewGuid().ToString();
            _logger = loggerFactory?.CreateLogger(LogCategories.Executor);
            _initialRetryPolicy = retryAttribute;
        }

        /// <summary>
        /// Executes a functoin with retries.
        /// </summary>
        /// <param name="executeFunc">The function execution action to perform.</param>
        /// <param name="input">Input value for a triggered function execution.</param>
        /// <returns>Returns functions execution result.</returns>
        public async Task<FunctionResult> ExecuteWithRetriesAsync(Func<TriggeredFunctionData, CancellationToken, Task<FunctionResult>> executeFunc, TriggeredFunctionData input, CancellationToken token)
        {
            return await ExecuteWithRetriesInternalAsync(executeFunc, input, token, _initialRetryPolicy);
        }

        private async Task<FunctionResult> ExecuteWithRetriesInternalAsync(Func<TriggeredFunctionData, CancellationToken, Task<FunctionResult>> executeFunc, TriggeredFunctionData input, CancellationToken token, RetryAttribute retryAttribute)
        {
            FunctionResult functionResult = null;

            var attempts = 0;
            SetRetry(input, 0);

            if (retryAttribute != null && retryAttribute.RetryCount != 0)
            {
                do
                {
                    if (token.IsCancellationRequested)
                    {
                        Log("Retries were canceled");
                    }

                    functionResult = await executeFunc(input, token);
                    if (functionResult.Exception is RetryException retryException)
                    {
                        // Start execute with retrries if RetryException returned
                        Log($"Function code returned retry settings", 0, retryException.RetryResult);
                        return await ExecuteWithRetriesInternalAsync(executeFunc, input, token, retryException.RetryResult as RetryAttribute);
                    }
                    if (functionResult.Exception != null)
                    {
                        Log(functionResult.Exception, attempts, retryAttribute);
                        if (attempts == retryAttribute.RetryCount)
                        {
                            Log("All retries have been exhausted");
                            break;
                        }
                        SetRetry(input, ++attempts);
                        await CreateDelay(attempts, retryAttribute);
                        continue;
                    }
                    break;
                }
                while (true);
            }
            else
            {
                functionResult = await executeFunc(input, token);
                if (functionResult.Exception is RetryException retryException)
                {
                    // Start execute with retrries if RetryException returned
                    Log($"Function code returned retry settings", 0, retryException.RetryResult);
                    return await ExecuteWithRetriesInternalAsync(executeFunc, input, token, retryException.RetryResult as RetryAttribute);
                }
            }
            return functionResult;
        }


        private void SetRetry(TriggeredFunctionData input, int retryCount)
        {
            if (input.TriggerDetails == null)
            {
                input.TriggerDetails = new Dictionary<string, string>()
                {
                    { RetryCountString, retryCount.ToString() }
                };
            }
            else
            {
                input.TriggerDetails[RetryCountString] = retryCount.ToString();
            }
        }

        private async Task CreateDelay(int attempts, RetryAttribute retryAttribute)
        {
            if (!retryAttribute.ExponentialBackoff)
            {
                await Task.Delay(retryAttribute.SleepDuration);
                Log("New linear retry attempt", attempts, retryAttribute);
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempts)));
                Log("New exponential retry attempt", attempts, retryAttribute);
            }
        }

        private void Log(Exception ex, int attempt, RetryAttribute retryAttribute)
        {
            _logger.LogError($"Id: '{_id}', Attempt: '{attempt}'" +
                $"RetryCount: '{retryAttribute.RetryCount}', SleepDuration: '{retryAttribute.SleepDuration}', ExponentialBackoff: '{retryAttribute.ExponentialBackoff}'", ex); 
        }

        private void Log(string message, LogLevel logLevel = LogLevel.Debug)
        {
            _logger.Log(logLevel, $"Id: '{_id}', Message: '{message}'");
        }

        private void Log(string message, int attempt, RetryAttribute retryAttribute, LogLevel logLevel = LogLevel.Debug)
        {
            _logger.Log(logLevel, $"Id: '{_id}', Message: '{message}', Attempt: '{attempt}'" +
                $"RetryCount: '{retryAttribute.RetryCount}', SleepDuration: '{retryAttribute.SleepDuration}', ExponentialBackoff: '{retryAttribute.ExponentialBackoff}'");
        }
    }
}
