// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using Microsoft.Azure.WebJobs.Host.Executors;
using System;

namespace Microsoft.Azure.WebJobs.Host.Abstractions
{
    /// <summary>
    /// Abstraction for ActivitySource to avoid dependency on System.Diagnostics.DiagnosticSource
    /// </summary>
    public interface IActivitySourceAbstraction
    {
        IDisposable? StartActivity(IFunctionInstanceEx functionInstance);
    }
}
