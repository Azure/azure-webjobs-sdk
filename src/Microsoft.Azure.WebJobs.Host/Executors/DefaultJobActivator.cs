// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class DefaultJobActivator : IJobActivator
    {
        public T CreateInstance<T>()
        {
            return Activator.CreateInstance<T>();
        }
    }
}
