// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Listeners
{
    public class Worker
    {
        private ITriggeredFunctionExecutor _triggeredFunctionExecutor;
        private CacheServer _cacheServer;

        public Worker(ITriggeredFunctionExecutor triggeredFunctionExecutor)
        {
            _triggeredFunctionExecutor = triggeredFunctionExecutor;
            _cacheServer = CacheServer.Instance;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            TriggeredFunctionData tData;
            while (!cancellationToken.IsCancellationRequested)
            {
                // TODO this is a very tight loop; put some sleep here or make the queue in cachserver use callbacks to come here
                if (_cacheServer.Triggers.Count > 0)
                {
                    if (_cacheServer.Triggers.TryDequeue(out CacheObjectMetadata metadata)) // TODO Don't dequeue, just peek or something or put to another queue as one atomic operation so we don't drop messages
                    {
                        _cacheServer.TriggersProcessedFromCache.Add(metadata.Uri); // TODO right now putting it as processed immediately but this is bad... we need to ensure failed functions don't just ghost the message
                        // TODO how do we ensure that the blobs with same name don't just keep skipping execution just because we have processed a similar named blob once? should there be a time limit thingy?
                        tData = new TriggeredFunctionData();
                        tData.TriggerDetails = new Dictionary<string, string>
                        {
                            { "name", metadata.Name },
                            { "CacheTrigger", true.ToString() }
                        };

                        if (_cacheServer.TryGetObjectByteRangesAndStream(metadata, out _, out MemoryStream mStream))
                        {
                            CacheTriggeredStream cStream = new CacheTriggeredStream(mStream, metadata);
                            tData.TriggerValue = cStream;
                            await _triggeredFunctionExecutor.TryExecuteAsync(tData, cancellationToken);
                            // TODO check if we ever get here... seems like this thread dies after one execution or something :/
                        }
                    }
                }
            }

            return 0;
        }
    }

    public class CacheListener : IListener
    {
        private Worker _worker;
        private FunctionDescriptor _functionDescriptor;
        private ITriggeredFunctionExecutor _triggeredFunctionExecutor;
        private List<Task> _tasks;

        public CacheListener(FunctionDescriptor functionDescriptor, ITriggeredFunctionExecutor triggerExecutor)
        {
            _functionDescriptor = functionDescriptor; // TODO might not need it 
            _triggeredFunctionExecutor = triggerExecutor;
            _worker = new Worker(_triggeredFunctionExecutor);
            _tasks = new List<Task>();
        }

        void IListener.Cancel()
        {
            // nop
        }

        void IDisposable.Dispose()
        {
            // nop
        }

        Task IListener.StartAsync(CancellationToken cancellationToken)
        {
            _tasks.Add(_worker.ExecuteAsync(cancellationToken));
            return Task.FromResult(0);
        }

        Task IListener.StopAsync(CancellationToken cancellationToken)
        {
            // nop
            return Task.FromResult(0);
        }
    }
}
