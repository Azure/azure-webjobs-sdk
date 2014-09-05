﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal interface IFunctionInstance
    {
        Guid Id { get; }

        Guid? ParentId { get; }

        ExecutionReason Reason { get; }

        IBindingSource BindingSource { get; }

        IInvoker Invoker { get; }

        FunctionDescriptor FunctionDescriptor { get; }

        MethodInfo Method { get; }
    }
}
