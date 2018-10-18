// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Host.Queues.Listeners
{
    internal class SharedQueueWatcher : IMessageEnqueuedWatcher
    {
        private readonly object _lock = new object();

        private readonly ConcurrentDictionary<string, ICollection<INotificationCommand>> _registrations =
            new ConcurrentDictionary<string, ICollection<INotificationCommand>>();

        public void Notify(string enqueuedInQueueName)
        {
            if (_registrations.TryGetValue(enqueuedInQueueName, out ICollection<INotificationCommand> queueRegistrations))
            {
                INotificationCommand[] registrations;

                lock (_lock)
                {
                    registrations = queueRegistrations.ToArray();
                }

                foreach (INotificationCommand registration in registrations)
                {
                    registration.Notify();
                }
            }
        }

        public void Register(string queueName, INotificationCommand notification)
        {
            _registrations.AddOrUpdate(queueName,
                new Collection<INotificationCommand> { notification },
                (i, existing) =>
                {
                    lock (_lock)
                    {
                        existing.Add(notification);
                    }

                    return existing;
                });
        }
    }
}
