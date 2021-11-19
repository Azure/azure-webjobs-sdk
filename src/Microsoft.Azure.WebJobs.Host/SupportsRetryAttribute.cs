// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Attribute applied to a <see cref="ITriggerBinding"/> implementation to declare that the trigger supports <see cref="RetryAttribute" />.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SupportsRetryAttribute : Attribute
    {
    }
}
