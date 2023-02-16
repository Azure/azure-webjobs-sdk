// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Provides trigger metadata.
    /// </summary>
    public interface ITriggerMetadataProvider
    {
        /// <summary>
        /// Gets the trigger metadata.
        /// </summary>
        /// <returns>The trigger metadata.</returns>
        IEnumerable<TriggerMetadata> GetTriggerMetadata();
    }
}
