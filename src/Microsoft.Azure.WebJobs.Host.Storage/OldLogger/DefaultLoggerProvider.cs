// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal class DefaultLoggerProvider : IHostInstanceLoggerProvider, IFunctionInstanceLoggerProvider, IFunctionOutputLoggerProvider
    {
        private bool _loggersSet;
        private IHostInstanceLogger _hostInstanceLogger;
        private IFunctionInstanceLogger _functionInstanceLogger;
        private IFunctionOutputLogger _functionOutputLogger;
        private ILoggerFactory _loggerFactory;
        private LegacyConfig _storageAccountProvider;

        public DefaultLoggerProvider(LegacyConfig storageAccountProvider, ILoggerFactory loggerFactory)
        {
            _storageAccountProvider = storageAccountProvider;
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

        private void EnsureLoggers()
        {
            if (_loggersSet)
            {
                return;
            }

            IFunctionInstanceLogger functionLogger = new FunctionInstanceLogger(_loggerFactory);


            if (_storageAccountProvider.Dashboard != null)
            {
                var dashboardAccount = _storageAccountProvider.GetDashboardStorageAccount();

                // Create logging against a live Azure account.
                var dashboardBlobClient = dashboardAccount.CreateCloudBlobClient();
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
        private Task EnsureLoggersAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => EnsureLoggers(), cancellationToken);
        }
    }
}
