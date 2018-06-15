// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Config;
using System.IO;
using System.Threading;
using System.Linq;
using System.Globalization;
using Xunit;
using Microsoft.Azure.WebJobs.Host.Timers;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    // $$$  Remove all this. See Blob_IfBoundToCloudBlockBlob_BindsAndCreatesContainerButNotBlob for an example of what it should be. 
    internal class FunctionalTest
    {        
        // $$$ Reconcile with TestJobHost.

        internal static TResult RunTrigger<TResult>(
            XStorageAccount account, 
            Type programType, 
            Action<TaskCompletionSource<TResult>> setTaskSource,
            IEnumerable<string> ignoreFailureFunctions = null)
        {
            TaskCompletionSource<TResult> src = new TaskCompletionSource<TResult>();
            setTaskSource(src);

            // var tracker = new TaskBackgroundExceptionHandler<TResult>(src);

            var host = new HostBuilder()
              .ConfigureDefaultTestHost(programType)
              .AddAzureStorage()
              .UseStorage(account)
              .ConfigureCatchFailures(src, ignoreFailureFunctions)
              .ConfigureServices(services =>
              {
                  // services.AddSingleton<IWebJobsExceptionHandler>(tracker);
              })
              .Build();

            try
            {
                using (var jobHost = host.GetJobHost())
                {
                    // start listeners. One of them will set the completition task
                    jobHost.Start();

                    var result = src.Task.AwaitWithTimeout(); // blocks

                    jobHost.Stop();

                    return result;
                }
            }
            catch (Exception exception)
            {
                // Unwrap 
                var e = exception;
                while (e != null)
                {
                    if (e is InvalidOperationException)
                    {
                        throw e;
                    }
                    e = e.InnerException;
                }
                throw;
            }
        }

        internal static void RunTrigger(XStorageAccount account, Type programType)
        {
            throw new NotImplementedException();
        }

        internal static object CreateConfigurationForCallFailure(XStorageAccount account, Type type, TaskCompletionSource<object> backgroundTaskSource)
        {
            throw new NotImplementedException();
        }

        internal static Exception RunTriggerFailure<TResult>(XStorageAccount account, Type programType, Action<TaskCompletionSource<TResult>> setTaskSource)
        {
            try
            {
                RunTrigger<TResult>(account, programType, setTaskSource);
            }
            catch (Exception e)
            {
                return e;
            }
            Assert.True(false, "Expected trigger to fail"); // throws
            return null;
        }

        internal static Exception CallFailure(XStorageAccount account, Type programType, MethodInfo methodInfo, object p)
        {
            throw new NotImplementedException();
        }

        internal static void Call(XStorageAccount account, Type programType, MethodInfo methodInfo, object arguments, Type[] cloudBlobStreamBinderTypes)
        {
            var host = new HostBuilder()
                .ConfigureDefaultTestHost(programType)
                .AddExtension(new CloudBlobStreamAdapterExtension(cloudBlobStreamBinderTypes))
                .AddAzureStorage()
                .UseStorage(account)
                .Build();

            var jobHost = host.GetJobHost();
            jobHost.Call(methodInfo, arguments);
        }

        internal static TResult Call<TResult>(XStorageAccount account, Type programType, MethodInfo methodInfo, IDictionary<string, object> arguments, Action<TaskCompletionSource<TResult>> setTaskSource)
        {
            TaskCompletionSource<TResult> src = new TaskCompletionSource<TResult>();
            setTaskSource(src);

            var host = new HostBuilder()
              .ConfigureDefaultTestHost(programType)
              .AddAzureStorage()
              .UseStorage(account)
              .Build();

            var jobHost = host.GetJobHost();
            jobHost.Call(methodInfo, arguments);

            return src.Task.Result;
        }
    }
}