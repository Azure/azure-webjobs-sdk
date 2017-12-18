// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal class DefaultLoggerProvider : IHostInstanceLoggerProvider, IFunctionInstanceLoggerProvider, IFunctionOutputLoggerProvider
    {
        private readonly IStorageAccountProvider _storageAccountProvider;

        private bool _loggersSet;
        private IHostInstanceLogger _hostInstanceLogger;
        private IFunctionInstanceLogger _functionInstanceLogger;
        private IFunctionOutputLogger _functionOutputLogger;
        private ILoggerFactory _loggerFactory;

        public DefaultLoggerProvider(IStorageAccountProvider storageAccountProvider, ILoggerFactory loggerFactory)
        {
            _storageAccountProvider = storageAccountProvider ?? throw new ArgumentNullException(nameof(storageAccountProvider));
            _loggerFactory = loggerFactory;
        }

        async Task<IHostInstanceLogger> IHostInstanceLoggerProvider.GetAsync(CancellationToken cancellationToken)
        {
            await EnsureLoggersAsync(cancellationToken);
            return _hostInstanceLogger;
        }

        async Task<IFunctionInstanceLogger> IFunctionInstanceLoggerProvider.GetAsync(CancellationToken cancellationToken)
        {
            await EnsureLoggersAsync(cancellationToken);
            return _functionInstanceLogger;
        }

        async Task<IFunctionOutputLogger> IFunctionOutputLoggerProvider.GetAsync(CancellationToken cancellationToken)
        {
            await EnsureLoggersAsync(cancellationToken);
            return _functionOutputLogger;
        }

        private async Task EnsureLoggersAsync(CancellationToken cancellationToken)
        {
            if (_loggersSet)
            {
                return;
            }

            IStorageAccount dashboardAccount = await _storageAccountProvider.GetDashboardAccountAsync(cancellationToken);
            IFunctionInstanceLogger functionLogger = new FunctionInstanceLogger(_loggerFactory);

            if (dashboardAccount != null)
            {
                // Create logging against a live Azure account.
                IStorageBlobClient dashboardBlobClient = dashboardAccount.CreateBlobClient();
                IPersistentQueueWriter<PersistentQueueMessage> queueWriter = new PersistentQueueWriter<PersistentQueueMessage>(dashboardBlobClient);
                PersistentQueueLogger queueLogger = new PersistentQueueLogger(queueWriter);
                _hostInstanceLogger = queueLogger;
                _functionInstanceLogger = new CompositeFunctionInstanceLogger(queueLogger, functionLogger);
                _functionOutputLogger = new BlobFunctionOutputLogger(dashboardBlobClient);
            }
            else
            {
                // No auxillary logging. Logging interfaces are nops or in-memory.
                _hostInstanceLogger = new NullHostInstanceLogger();
                _functionInstanceLogger = functionLogger;
                _functionOutputLogger = new ConsoleFunctionOutputLogger();
            }

            _loggersSet = true;
        }
    }
}
