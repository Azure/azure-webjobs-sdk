// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    /// <summary>
    /// Factory for creating <see cref="IInstanceServicesProvider"/> instances.
    /// </summary>
    public interface IInstanceServicesProviderFactory
    {
        IInstanceServicesProvider CreateInstanceServicesProvider(FunctionInstanceFactoryContext functionInstance);
    }
}