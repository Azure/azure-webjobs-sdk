﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal interface IInvoker
    {
        IReadOnlyList<string> ParameterNames { get; }

        // The cancellation token, if any, is provided along with the other parameters.
        Task InvokeAsync(object[] parameters);
    }
}
