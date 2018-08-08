// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs
{
    public interface IWebJobsExtensionBuilder
    {
        /// <summary>
        /// Gets the <see cref="IServiceCollection"/> where WebJobs extension services are configured.
        /// </summary>
        IServiceCollection Services { get; }

        /// <summary>
        /// The name of the extension being configured by this builder.
        /// </summary>
        string ExtensionName { get; }
    }
}
