// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public class JobHost<TProgram> : JobHost
    {
        private readonly IJobActivator _jobActivator;

        public JobHost(
            IOptions<JobHostOptions> options, 
            IJobHostContextFactory contextFactory,
            IJobActivator jobActivator)
            : base(options, contextFactory)
        {
            _jobActivator = jobActivator;
        }

        public void Call(string methodName)
        {
            base.Call(typeof(TProgram).GetMethod(methodName));
        }

        public void Call(string methodName, object arguments)
        {
            base.Call(typeof(TProgram).GetMethod(methodName), arguments);
        }

        public void Call(string methodName, IDictionary<string, object> arguments)
        {
            base.Call(typeof(TProgram).GetMethod(methodName), arguments);
        }

        public Task CallAsync(string methodName, object arguments)
        {
            return base.CallAsync(typeof(TProgram).GetMethod(methodName), arguments);
        }

        // Start listeners and run until the Task source is set. 
        public async Task<TResult> RunTriggerAsync<TResult>(TaskCompletionSource<TResult> taskSource= null)
        {
            // Program was registered with the job activator, so we can get it
            TProgram prog = _jobActivator.CreateInstance<TProgram>();
            if (taskSource == null)
            {
                var progResult = prog as IProgramWithResult<TResult>;
                taskSource = new TaskCompletionSource<TResult>();
                progResult.TaskSource = taskSource;
            }

            TResult result = default(TResult);
            // Act
            using (this)
            {
                this.Start();
                // Assert
                result = await TestHelpers.AwaitWithTimeout(taskSource);
            }
            return result;
        }

        // Helper for quickly testing indexing errors 
        public void AssertIndexingError(string methodName, string expectedErrorMessage)
        {
            try
            {
                // Indexing is lazy, so must actually try a call first. 
                this.Call(methodName);
            }
            catch (FunctionIndexingException e)
            {
                string functionName = typeof(TProgram).Name + "." + methodName;
                Assert.Equal("Error indexing method '" + functionName + "'", e.Message);
                Assert.True(e.InnerException.Message.Contains(expectedErrorMessage));
                return;
            }
            Assert.True(false, "Invoker should have failed");
        }
    }

    // $$$ Meanth to simplify some tests - is this worth it? 
    public interface IProgramWithResult<TResult>
    {
        TaskCompletionSource<TResult> TaskSource { get; set; }
    }
}
