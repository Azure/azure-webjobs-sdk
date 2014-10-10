// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    /// <summary>
    /// Implementation of <see cref="IHostIdProvider"/> designed to defer throwing an exception till the first time
    /// actual Host Id value is needed.
    /// </summary>
    /// <remarks>
    /// Host Id is an optional feature of SDK and in some cases it may function correctly without it.
    /// This strategy is used to detect missing configuration settings at places where this feature is actually consumed.
    /// </remarks>
    internal class MissingHostIdProvider : IHostIdProvider
    {
        /// <inheritdoc />
        public Task<string> GetHostIdAsync(IEnumerable<MethodInfo> indexedMethods, CancellationToken cancellationToken)
        {
            const string errorMessage = "Using the AzureWebJobsDashboard connection string requires either " +
                @"setting JobHostConfiguration.HostId or providing an ""AzureWebJobsStorage"" connection string.";
            throw new InvalidOperationException(errorMessage);
        }
    }
}
