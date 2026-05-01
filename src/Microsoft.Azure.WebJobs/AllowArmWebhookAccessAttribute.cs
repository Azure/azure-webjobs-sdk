// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Applied to an <see cref="IExtensionConfigProvider"/> that registers a webhook handler
    /// to indicate that the extension allows its webhook endpoints to be accessed via ARM.
    /// </summary>
    /// <remarks>
    /// Without this attribute, all ARM-bridged requests to the extension's webhook route will be
    /// blocked.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class AllowArmWebhookAccessAttribute : Attribute
    {
    }
}
