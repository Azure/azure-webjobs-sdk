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

        public static T ReadFromBinaryFile<T>(string filePath)
        {
            using (Stream stream = File.Open(filePath, FileMode.Open))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                return (T)binaryFormatter.Deserialize(stream);
            }
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            bool done = true;
            TriggeredFunctionData tData;
            while (!cancellationToken.IsCancellationRequested && done)
            {
                Thread.Sleep(60000); // TODO remove this or lower this
                if (_cacheServer.Triggers.Count > 0)
                {
                    if (_cacheServer.Triggers.TryDequeue(out CacheObjectMetadata metadata))
                    {
                        tData = new TriggeredFunctionData();
                        done = false;
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
                            done = true;
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
