// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal interface IJobMethodInvoker
    {
        Task InvokeAsync(MethodInfo methodInfo, IReadOnlyDictionary<string, object> parameters, CancellationToken cancellationToken);

        MethodInfo GetMethodInfoForInvoke(string methodName);
    }
}