// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    // $$$  Remove all this. See Blob_IfBoundToCloudBlockBlob_BindsAndCreatesContainerButNotBlob for an example of what it should be. 
    internal class FunctionalTest
    {
        internal static TResult RunTrigger<TResult>(XStorageAccount account, Type programType, Action<TaskCompletionSource<TResult>> setTaskSource, IEnumerable<string> ignoreFailureFunctions)
        {
            throw new NotImplementedException();
        }

        internal static TResult RunTrigger<TResult>(XStorageAccount account, Type programType, Action<TaskCompletionSource<TResult>> setTaskSource)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        internal static Exception CallFailure(XStorageAccount account, Type programType, MethodInfo methodInfo, object p)
        {
            throw new NotImplementedException();
        }

        internal static void Call(XStorageAccount account, Type programType, MethodInfo methodInfo, object arguments, Type[] cloudBlobStreamBinderTypes)
        {
            throw new NotImplementedException();
        }

        internal static TResult Call<TResult>(XStorageAccount account, Type programType, MethodInfo methodInfo, IDictionary<string, object> arguments, Action<TaskCompletionSource<TResult>> setTaskSource)
        {
            throw new NotImplementedException();
        }

        internal static object CreateConfigurationForManualCompletion<TResult>(XStorageAccount xStorageAccount, Type programType, TaskCompletionSource<TResult> functionTaskSource)
        {
            throw new NotImplementedException();
        }
    }
}