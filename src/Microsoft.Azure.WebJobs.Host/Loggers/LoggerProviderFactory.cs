// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal sealed class LoggerProviderFactory
    {
        private readonly IStorageAccountProvider _storageAccountProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IAsyncCollector<FunctionInstanceLogEntry> _registeredFunctionEventCollector;
        private readonly Lazy<object> _loggerProvider;


        public LoggerProviderFactory(IStorageAccountProvider storageAccountProvider,
            ILoggerFactory loggerFactory,
            IAsyncCollector<FunctionInstanceLogEntry> registeredFunctionEventCollector = null)
        {
            _storageAccountProvider = storageAccountProvider;
            _loggerFactory = loggerFactory;
            _registeredFunctionEventCollector = registeredFunctionEventCollector;

            _loggerProvider = new Lazy<object>(CreateLoggerProvider);
        }

        private object CreateLoggerProvider()
        {
            bool hasFastTableHook = _registeredFunctionEventCollector != null;
            bool noDashboardStorage = _storageAccountProvider.DashboardConnectionString == null;

            if (hasFastTableHook && noDashboardStorage)
            {
                return new FastTableLoggerProvider(_loggerFactory);
            }

            return new DefaultLoggerProvider(_storageAccountProvider, _loggerFactory);
        }

        public T GetLoggerProvider<T>() where T : class => _loggerProvider.Value as T;
    }
}
