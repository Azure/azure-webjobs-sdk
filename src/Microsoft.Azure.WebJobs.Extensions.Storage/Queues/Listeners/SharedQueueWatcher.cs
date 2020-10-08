// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Host.Queues.Listeners
{
    internal class SharedQueueWatcher : IMessageEnqueuedWatcher
    {
        private readonly ConcurrentDictionary<string, INotificationCommand[]> _registrations =
            new ConcurrentDictionary<string, INotificationCommand[]>();

        public void Notify(string enqueuedInQueueName)
        {
            INotificationCommand[] queueRegistrations;

            if (_registrations.TryGetValue(enqueuedInQueueName, out queueRegistrations))
            {
                foreach (INotificationCommand registration in queueRegistrations)
                {
                    registration.Notify();
                }
            }
        }

        public void Register(string queueName, INotificationCommand notification)
        {
            _registrations.AddOrUpdate(queueName, new[] { notification },
                (i, existing) =>
                {
                    var updated = new INotificationCommand[existing.Length + 1];
                    existing.CopyTo(updated, 0);
                    updated[existing.Length] = notification;
                    return updated;
                });
        }
    }
}
