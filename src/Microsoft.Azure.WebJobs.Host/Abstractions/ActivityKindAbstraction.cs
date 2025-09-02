// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Abstractions
{
    /// <summary>
    /// Abstraction for activity kinds to avoid dependency on System.Diagnostics.DiagnosticSource
    /// </summary>
    public enum ActivityKindAbstraction
    {
        Internal = 0,
        Server = 1,
        Client = 2,
        Producer = 3,
        Consumer = 4
    }
}
