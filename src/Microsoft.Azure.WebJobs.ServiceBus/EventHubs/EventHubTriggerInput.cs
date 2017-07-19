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

        private bool _isSingleDispatch = false;

        internal static Dictionary<string, List<EventData>> GroupedEvents { get; set; }

        internal static Dictionary<int, List<EventData>> SlottedEvents { get; set; }

        internal EventData[] Events { get; set; }
        internal PartitionContext PartitionContext { get; set; }

        internal int OrderedEventSlotCount { get; set; }

        public static EventHubTriggerInput New(EventData eventData, bool isSingleDispatch)
        {
            return new EventHubTriggerInput
            {
                PartitionContext = null,
                Events = new EventData[]
                {
                      eventData
                },
                _selector = 0,
                _isSingleDispatch = isSingleDispatch
            };
        }

        public bool IsSingleDispatch
        {
            get
            {
                return _isSingleDispatch;
            }
        }

        public EventHubTriggerInput GetSingleEventTriggerInput(int idx)
        {
            return new EventHubTriggerInput
            {
                Events = this.Events,
                PartitionContext = this.PartitionContext,
                _selector = idx,
                _isSingleDispatch = true
            };
        }

        public EventHubTriggerInput GetOrderedBatchEventTriggerInput(int idx)
        {
            if (SlottedEvents.ContainsKey(idx))
            {
                return new EventHubTriggerInput
                {
                    Events = SlottedEvents[idx].ToArray(),
                    PartitionContext = this.PartitionContext,
                    _selector = idx
                };
            }

            return null;
        }

        public EventData GetSingleEventData()
        {
            return this.Events[this._selector];
        }

        public List<EventData> GetBatchEventData()
        {
            return SlottedEvents[this._selector];
        }

        public void CreatePartitionKeyOrdering()
        {
            GroupedEvents = this.Events.GroupBy(e => e.PartitionKey).ToDictionary(g => g.Key, g => g.ToList());
            SlottedEvents = new Dictionary<int, List<EventData>>();

            /*
            for (int i = 0; i < this.OrderedEventSlotCount; ++i)
            {
                SlottedEvents.Add(i, new List<EventData>());
            }
            */

            foreach (var groupedEvent in GroupedEvents)
            {
                int slotId = GetNextSlot();

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

        private static int GetNextSlot()
        {
            if (SlottedEvents.Count > 2)
            {
                return SlottedEvents.Aggregate((l, r) => (l.Value.Any() && r.Value.Any() && (l.Value.Count > r.Value.Count)) ? l : r).Key;
            }

            return (SlottedEvents.Count == 0) ? 0 : 1;
        }
    }
}