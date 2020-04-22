﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Listeners
{

    public class CacheListener : IListener
    {
        private CacheServer _cacheServer;
        private Task _task;
        private CancellationToken _cancellationToken;
        private ConcurrentDictionary<string, List<ITriggeredFunctionExecutor>> _executors;

        private CacheListener()
        {
            _cacheServer = CacheServer.Instance;
            _task = null;
            _executors = new ConcurrentDictionary<string, List<ITriggeredFunctionExecutor>>();
        }

        private static CacheListener instance = null;

        public static CacheListener Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new CacheListener();
                }
                return instance;
            }
        }

        public void Register(string containerName, ITriggeredFunctionExecutor executor)
        {
            if (!_executors.ContainsKey(containerName))
            {
                if (!_executors.TryAdd(containerName, new List<ITriggeredFunctionExecutor>()))
                {
                    throw new Exception("Unable to add function executor to dictionary of executors");
                }
            }
            
            _executors[containerName].Add(executor);
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
            // Only start once
            if (_task == null)
            {
                _cancellationToken = cancellationToken;
                _cacheServer.Triggers.OnEnqueue += Triggers_OnEnqueue;
                _task = ExecuteAsync();
            }

            return Task.FromResult(0);
        }

        private async void Triggers_OnEnqueue(object sender, EventArgs e)
        {
            CacheListener cacheListener = CacheListener.instance;
            await cacheListener.HandleCacheEvent();
        }

        Task IListener.StopAsync(CancellationToken cancellationToken)
        {
            // nop
            return Task.FromResult(0);
        }

        private void ResetProcessedList()
        {
            // There is no Clear() method for ConcurrentBag so we create a new one and atomically replace the one in CacheServer with it
            ConcurrentBag<string> newBag = new ConcurrentBag<string>();
            _cacheServer.TriggersProcessedFromCache = newBag; // Assignment is atomic
        }

        private async Task HandleCacheEvent()
        {
            // TODO this is a very tight loop; put some sleep here or make the queue in cachserver use callbacks to come here
            if (_cacheServer.Triggers.Count > 0)
            {
                // TODO Don't dequeue, just peek or something or put to another queue as one atomic operation so we don't drop messages
                if (_cacheServer.Triggers.TryDequeue(out CacheObjectMetadata metadata))
                {
                    string containerName = metadata.ContainerName;

                    if (_executors.ContainsKey(containerName))
                    {
                        if (_executors.TryGetValue(containerName, out List<ITriggeredFunctionExecutor> executors))
                        {
                            // TODO do this asynchronously for all executors? Not sure what the semantics are if more than one function in the application is listening on the same blob
                            foreach (ITriggeredFunctionExecutor executor in executors)
                            {
                                TriggeredFunctionData tData = new TriggeredFunctionData();
                                tData.TriggerDetails = new Dictionary<string, string>
                                {
                                    { "name", metadata.Name }
                                };

                                CacheTriggeredInput cacheTriggeredInput = new CacheTriggeredInput(metadata);
                                tData.TriggerValue = cacheTriggeredInput;

                                await executor.TryExecuteAsync(tData, _cancellationToken);

                                // TODO how do we ensure that the blobs with same name don't just keep skipping execution just because we have processed a similar named blob once? should there be a time limit thingy?
                                // Also, this may result in double processing if by the time the *actual* invocation from blob comes, the cache trigger is still not done processing
                                _cacheServer.TriggersProcessedFromCache.Add(metadata.Uri);
                            }
                        }
                    }
                }
            }
        }

        public async Task<int> ExecuteAsync()
        {
            await Task.Yield();

            while (!_cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(60 * 1000); // Sleep for 1 min
                ResetProcessedList();
            }

            return 0;
        }
    }
}
