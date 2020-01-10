// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;
using System.Web;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Threading;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.Azure.WebJobs.Host.Queues;

namespace Microsoft.Azure.WebJobs.Host.Blobs
{
    internal class EventGridHttpRequestProcessor
    {
        private ILogger _logger;
        private CloudQueue _cloudQueue;

        public EventGridHttpRequestProcessor(CloudQueue cloudQueue, string triggerCategoryName, ILoggerFactory loggerFactory)
        {
            _cloudQueue = cloudQueue;
            _logger = loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory(triggerCategoryName));
        }

        public async Task<HttpResponseMessage> ProcessHttpRequestAsync(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var functionId = HttpUtility.ParseQueryString(req.RequestUri.Query)["functionId"];

            IEnumerable<string> eventTypeHeaders = null;
            string eventTypeHeader = null;
            if (req.Headers.TryGetValues("aeg-event-type", out eventTypeHeaders))
            {
                eventTypeHeader = eventTypeHeaders.First();
            }

            if (String.Equals(eventTypeHeader, "SubscriptionValidation", StringComparison.OrdinalIgnoreCase))
            {
                string jsonArray = await req.Content.ReadAsStringAsync();
                SubscriptionValidationEvent validationEvent = null;
                List<JObject> events = JsonConvert.DeserializeObject<List<JObject>>(jsonArray);
                // TODO remove unnecessary serialization
                validationEvent = ((JObject)events[0]["data"]).ToObject<SubscriptionValidationEvent>();
                SubscriptionValidationResponse validationResponse = new SubscriptionValidationResponse { ValidationResponse = validationEvent.ValidationCode };
                var returnMessage = new HttpResponseMessage(HttpStatusCode.OK);
                returnMessage.Content = new StringContent(JsonConvert.SerializeObject(validationResponse));
                _logger.LogInformation($"perform handshake with eventGrid for function: {functionId}");
                return returnMessage;
            }
            else if (String.Equals(eventTypeHeader, "Notification", StringComparison.OrdinalIgnoreCase))
            {
                JArray events = null;
                string requestContent = await req.Content.ReadAsStringAsync();
                var token = JToken.Parse(requestContent);
                if (token.Type == JTokenType.Array)
                {
                    // eventgrid schema
                    events = (JArray)token;
                }
                else if (token.Type == JTokenType.Object)
                {
                    // cloudevent schema
                    events = new JArray
                    {
                        token
                    };
                }

                foreach (JObject jo in events)
                {
                    jo["functionId"] = functionId;
                    var queueMessage = new CloudQueueMessage(jo.ToString());
                    await _cloudQueue.AddMessageAndCreateIfNotExistsAsync(queueMessage, CancellationToken.None);
                }

                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }
            else if (String.Equals(eventTypeHeader, "Unsubscribe", StringComparison.OrdinalIgnoreCase))
            {
                // TODO disable function?
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }
    }

    internal class SubscriptionValidationResponse
    {
        [JsonProperty(PropertyName = "validationResponse")]
        public string ValidationResponse { get; set; }
    }

    internal class SubscriptionValidationEvent
    {
        [JsonProperty(PropertyName = "validationCode")]
        public string ValidationCode { get; set; }
    }
}
