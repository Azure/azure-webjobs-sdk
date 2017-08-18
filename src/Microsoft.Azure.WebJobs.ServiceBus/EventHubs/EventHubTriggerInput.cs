// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
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

        private Object thisLock = new Object();

        internal Dictionary<int, List<EventData>> SlottedEvents { get; set; }

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

        public int SelectorId
        {
            get
            {
                return _selector;
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
            lock (thisLock)
            {
                if (SlottedEvents.ContainsKey(idx) && SlottedEvents[idx].Any())
                {
                    return new EventHubTriggerInput
                    {
                        Events = SlottedEvents[idx].ToArray(),
                        PartitionContext = this.PartitionContext,
                        _selector = idx,
                        _isSingleDispatch = false
                    };
                }
            }

            return null;
        }

        public EventData GetSingleEventData()
        {
            return this.Events[this._selector];
        }

        public EventData[] GetBatchEventData()
        {
            lock (thisLock)
            {
                return this.Events;
            }
        }

        public void CreatePartitionKeyOrdering()
        {
            lock (thisLock)
            {
                // GroupedEvents = this.Events.GroupBy(e => e.PartitionKey).ToDictionary(g => g.Key, g => g.ToList());
                var groupedEvents = (from events in this.Events
                                     group events by events.PartitionKey
                                     into groupedEvent
                                     select groupedEvent).ToDictionary(g => g.Key, g => g.ToList());

                SlottedEvents = new Dictionary<int, List<EventData>>();

                for (int i = 0; i < this.OrderedEventSlotCount; ++i)
                {
                    if (!SlottedEvents.ContainsKey(i))
                    {
                        SlottedEvents.Add(i, new List<EventData>());
                    }
                }

                foreach (var groupedEvent in groupedEvents)
                {
                    int slotId = GetNextSlot();
                    SlottedEvents[slotId].AddRange(groupedEvent.Value);
                }
            }
        }

        private int GetNextSlot()
        {
            lock (thisLock)
            {
                return SlottedEvents.Aggregate((l, r) => (l.Value.Count < r.Value.Count) ? l : r).Key;
            }

            /*
            if (SlottedEvents.Count > 2)
            {
                return SlottedEvents.Aggregate((l, r) => (l.Value.Any() && r.Value.Any() && (l.Value.Count > r.Value.Count)) ? l : r).Key;
            }

            return (SlottedEvents.Count == 0) ? 0 : 1;
            */
        }
    }
}