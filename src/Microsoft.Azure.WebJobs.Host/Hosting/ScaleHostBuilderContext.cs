// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Host.Hosting
{
    /// <summary>
    /// Context containing triggers metadata needed to be confiured for scale in an extension.
    /// </summary>
    public class ScaleHostBuilderContext : WebJobsBuilderContext
    {
        /// <summary>
        /// Gets or sets <see cref="IEnumerable"/> of <see cref="TriggerMetadata"/> needed to be configured for scale.
        /// </summary>
        public IEnumerable<TriggerMetadata> TriggersMetadata{ get; set; }
    }
}
