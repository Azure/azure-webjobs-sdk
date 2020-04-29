// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net.Http;

namespace Microsoft.Azure.WebJobs.Extensions.Storage
{
    public interface IDelegatingHandlerFactory
    {
        DelegatingHandler Create();
    }
}
