// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host
{
    public class FunctionInvocationCanceledException : FunctionInvocationException
    {
        public FunctionInvocationCanceledException(string invocationId, Exception innerException)
            : base($"The invocation request with Id '{invocationId}' was canceled.", innerException) { }
    }
}