// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host.Abstractions
{
    /// <summary>
    /// Abstraction for Activity to avoid dependency on System.Diagnostics.DiagnosticSource
    /// </summary>
    public interface IActivityAbstraction : IDisposable
    {
        string DisplayName { get; set; }
    }
}
