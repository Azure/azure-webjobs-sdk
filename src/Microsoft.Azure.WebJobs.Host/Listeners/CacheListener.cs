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
        private static TriggeredFunctionData stuff = null;

        public Worker(ITriggeredFunctionExecutor triggeredFunctionExecutor)
        {
            _triggeredFunctionExecutor = triggeredFunctionExecutor;
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
            while (!cancellationToken.IsCancellationRequested)
            {

                //return Task.FromResult(0);

                // Include the queue details here.
                //IDictionary<string, string> details = new Dictionary<string, string>();

                if (Worker.stuff != null)
                {
                    Thread.Sleep(10000);
                    Microsoft.Azure.Storage.CloudBlockBlob c =
                    await _triggeredFunctionExecutor.TryExecuteAsync(stuff, cancellationToken);
                }
                //else
                //{
                //    try
                //    {
                //        Worker.stuff = ReadFromBinaryFile<TriggeredFunctionData>("D:\\home\\funcdata.obj");
                //    }
                //    catch
                //    {
                //        Worker.stuff = null;
                //    }
                //}
                Thread.Sleep(10000);
            }

            return 0;
        }

        public static void StoreStuff(TriggeredFunctionData tempInput)
        {
            if (Worker.stuff == null)
            {
                Worker.stuff = tempInput;
            }
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
            _functionDescriptor = functionDescriptor;
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
