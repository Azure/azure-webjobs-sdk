// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Provides triggers metadata.
    /// </summary>
    public interface ITriggerMetadataProvider
    {
        /// <summary>
        /// Gets triggers metadata by trigger type.
        /// </summary>
        /// <param name="triggerType">Trigger type.</param>
        public IEnumerable<TriggerMetadata> GetTriggersMetadata(string triggerType);
    }
}
