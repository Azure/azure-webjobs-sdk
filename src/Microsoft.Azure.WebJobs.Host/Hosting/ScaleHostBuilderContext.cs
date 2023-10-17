// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Host.Hosting
{
    /// <summary>
    /// Context containing triggers metadata needed to be confiured for scale in an extension.
    /// </summary>
    public class ScaleHostBuilderContext : WebJobsBuilderContext
    {
        private IEnumerable<TriggerMetadata> _triggersMetadata;

        public ScaleHostBuilderContext(IEnumerable<TriggerMetadata> triggersMetadata)
        {
            _triggersMetadata = triggersMetadata;
        }

        /// <summary>
        /// Returns <see cref="IEnumerable"> of <see cref="TriggerMetadata"/> by trigger type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public IEnumerable<TriggerMetadata> GetTriggersMetadata(string type) => _triggersMetadata.Where(x => x.Type == type);
    }
}
