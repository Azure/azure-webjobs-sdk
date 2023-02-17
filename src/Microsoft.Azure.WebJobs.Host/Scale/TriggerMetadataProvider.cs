// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    internal class TriggerMetadataProvider : ITriggerMetadataProvider
    {
        private readonly IEnumerable<TriggerMetadata> _triggerMetadata;

        public TriggerMetadataProvider(IEnumerable<TriggerMetadata> triggerMetadata)
        {
            _triggerMetadata = triggerMetadata;
        }

        public IEnumerable<TriggerMetadata> GetTriggerMetadata()
        {
            return _triggerMetadata;
        }
    }
}
