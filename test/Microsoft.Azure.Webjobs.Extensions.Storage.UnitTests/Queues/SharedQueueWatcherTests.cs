// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Storage.UnitTests.Queues
{
    public class SharedQueueWatcherTests
    {
        [Fact]
        public void SharedQueueWatcher_IfConcurrentRegistrations_RegistersThemAll()
        {
            const string queue = "queue";
            const int countPerThread = 1000;
            const int threadCount = 4;
            const int commandCount = countPerThread * threadCount;

            Mock<INotificationCommand>[] commands = new Mock<INotificationCommand>[commandCount];
            for (int i = 0; i < commandCount; i++)
            {
                commands[i] = new Mock<INotificationCommand>(MockBehavior.Strict);
                commands[i].Setup(cmd => cmd.Notify());
            }

            var wait = new ManualResetEvent(false);

            SharedQueueWatcher watcher = new SharedQueueWatcher();

            Thread[] threads = new Thread[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                var startAt = i * countPerThread;
                threads[i] = new Thread(() =>
                {
                   wait.WaitOne();
                    for (int j = 0; j < countPerThread; j++)
                    {
                        watcher.Register(queue, commands[startAt + j].Object);
                    }
                });
            }

            foreach (var thread in threads)
            {
                thread.Start();
            }

            wait.Set();

            foreach (var thread in threads)
            {
                thread.Join();
            }

            watcher.Notify(queue);

            foreach (var mock in commands)
            {
                mock.Verify();
            }
        }
    }
}