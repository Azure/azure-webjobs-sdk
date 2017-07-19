// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // The core object we get when an EventHub is triggered. 
    // This gets converted to the user type (EventData, string, poco, etc) 
    internal sealed class EventHubTriggerInput      
    {        
        // If != -1, then only process a single event in this batch. 
        private int _selector = -1;

        internal Dictionary<string, List<EventData>> GroupedEvents { get; set; }

        internal Dictionary<int, List<EventData>> SlottedEvents { get; set; }

        internal EventData[] Events { get; set; }
        internal PartitionContext PartitionContext { get; set; }

        internal int OrderedEventSlotCount { get; set; }

        public static EventHubTriggerInput New(EventData eventData)
        {
            return new EventHubTriggerInput
            {
                PartitionContext = null,
                Events = new EventData[]
                {
                      eventData
                },
                _selector = 0,
            };
        }

        public bool IsSingleDispatch
        {
            get
            {
                return _selector != -1;
            }
        }

        public EventHubTriggerInput GetSingleEventTriggerInput(int idx)
        {
            return new EventHubTriggerInput
            {
                Events = this.Events,
                PartitionContext = this.PartitionContext,
                _selector = idx
            };
        }

        public EventHubTriggerInput GetOrderedBatchEventTriggerInput(int idx)
        {
            return new EventHubTriggerInput
            {
                Events = this.SlottedEvents[idx].ToArray(),
                PartitionContext = this.PartitionContext,
                _selector = idx
            };
        }

        public EventData GetSingleEventData()
        {
            return this.Events[this._selector];
        }

        public List<EventData> GetBatchEventData()
        {
            return this.SlottedEvents[this._selector];
        }

        public void CreatePartitionKeyOrdering()
        {
            this.GroupedEvents = this.Events.GroupBy(e => e.PartitionKey).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var groupedEvent in GroupedEvents)
            {
                int slotId = this.GetNextSlot();

                if (SlottedEvents.ContainsKey(slotId))
                {
                    SlottedEvents[slotId].AddRange(groupedEvent.Value);
                }
                else
                {
                    SlottedEvents.Add(slotId, groupedEvent.Value);
                }
            }
        }

        private int GetNextSlot()
        {
            return this.SlottedEvents.Aggregate((l, r) => l.Value.Count > r.Value.Count ? l : r).Key;
        }
    }
}