// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Indexers
{
    // Provide tooling support for the default binders. 
    internal class DefaultExtensions : ExtensionBase
    {
        private enum Direction // $$$ Move to more common place
        {
            @in = 1, // Same ordering as FileAccess
            @out,
            inout
        }

        protected internal override IEnumerable<Type> ExposedAttributes
        {
            get
            {
                return new Type[]
                {
                    typeof(BlobAttribute),
                    typeof(BlobTriggerAttribute),
                    typeof(QueueAttribute),
                    typeof(QueueTriggerAttribute),
                    typeof(TableAttribute)
                };
            }
        }

        public override Task InitAsync(JobHostConfiguration config, JObject metadata)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (metadata != null)
            {
                JObject configSection = (JObject)metadata["queues"];
                JToken value = null;
                if (configSection != null)
                {
                    if (configSection.TryGetValue("maxPollingInterval", out value))
                    {
                        config.Queues.MaxPollingInterval = TimeSpan.FromMilliseconds((int)value);
                    }
                    if (configSection.TryGetValue("batchSize", out value))
                    {
                        config.Queues.BatchSize = (int)value;
                    }
                    if (configSection.TryGetValue("maxDequeueCount", out value))
                    {
                        config.Queues.MaxDequeueCount = (int)value;
                    }
                    if (configSection.TryGetValue("newBatchThreshold", out value))
                    {
                        config.Queues.NewBatchThreshold = (int)value;
                    }
                }
            }

            return Task.FromResult(0);
        }

        public override Type GetDefaultType(FileAccess access, Cardinality cardinality, DataType dataType, Attribute attribute)
        {
            if (attribute == null)
            {
                throw new ArgumentNullException("attribute");
            }
            // $$$ Switch binders over to rule-based and we can get rid of the special casing here. 
            var attributeType = attribute.GetType();
            if (attributeType == typeof(BlobAttribute) || attributeType == typeof(BlobTriggerAttribute))
            {
                return typeof(Stream);
            }
            else if (attributeType == typeof(TableAttribute))
            {
                if (access == FileAccess.Write)
                {
                    return typeof(IAsyncCollector<JObject>);
                }
            }

            // remaining cases use rule-based bindings. 
            return base.GetDefaultType(access, cardinality, dataType, attribute);
        }

        public override Attribute[] GetAttributes(Type attributeType, JObject metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException("metadata");
            }

            var attributes = base.GetAttributes(attributeType, metadata);            

            // $$$ Put Connnection property on the SDK attributes and we can get rid of this. 
            JToken token;
            if (metadata.TryGetValue("connection", StringComparison.OrdinalIgnoreCase, out token))
            {
                string account = token.ToString();
                if (!string.IsNullOrWhiteSpace(account))
                {
                    return new Attribute[]
                    {
                        attributes[0],
                        new StorageAccountAttribute(account)
                    };
                }
            }
            return attributes;
        }
    }
}