// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.Storage.Queue;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Queues
{
    /// <summary>
    /// Tracks causality via JSON formatted queue message content. 
    /// Adds an extra field to the JSON object for the parent guid name.
    /// </summary>
    /// <remarks>
    /// Important that this class can interoperate with external queue messages, 
    /// so be resilient to a missing guid marker. 
    /// Can we switch to some auxiliary table? Beware, CloudQueueMessage. 
    /// Id is not filled out until after the message is queued, 
    /// but then there's a race between updating the aux storage and another function picking up the message.
    /// </remarks>
    internal static class QueueCausalityManager
    {
        private const string ParentGuidFieldName = "$AzureWebJobsParentId";

        public static void SetOwner(Guid functionOwner, JObject token)
        {
            if (token == null)
            {
                throw new ArgumentNullException("token");
            }

            if (!Guid.Equals(Guid.Empty, functionOwner))
            {
                token[ParentGuidFieldName] = functionOwner.ToString();
            }
        }

        [DebuggerNonUserCode]
        public static Guid? GetOwner(CloudQueueMessage msg)
        {
            string text = msg.TryGetAsString();

            if (text == null || !JsonSerialization.IsJsonObject(text))
            {
                return null;
            }

            try
            {
                using (var stringReader = new StringReader(text))
                {
                    using (var reader = JsonSerialization.CreateJsonTextReader(stringReader))
                    {
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.PropertyName && reader.Value.ToString() == ParentGuidFieldName)
                            {
                                if (reader.Read())
                                {
                                    if (reader.TokenType == JsonToken.String)
                                    {
                                        Guid guid;
                                        if (Guid.TryParse(reader.Value.ToString(), out guid))
                                        {
                                            return guid;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }
    }
}
