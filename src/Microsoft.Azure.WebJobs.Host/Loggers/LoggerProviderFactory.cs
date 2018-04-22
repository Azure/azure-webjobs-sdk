// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal sealed class LoggerProviderFactory
    {
        private readonly IStorageAccountProvider _storageAccountProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly bool _hasFastTableHook;
        private readonly Lazy<object> _loggerProvider;

        public LoggerProviderFactory(IStorageAccountProvider storageAccountProvider,
            ILoggerFactory loggerFactory,
            IEventCollectorFactory fastLoggerFactory = null)
        {
            _storageAccountProvider = storageAccountProvider;
            _loggerFactory = loggerFactory;
            _hasFastTableHook = fastLoggerFactory != null;

            _loggerProvider = new Lazy<object>(CreateLoggerProvider);
        }

        private object CreateLoggerProvider()
        {
            bool noDashboardStorage = _storageAccountProvider.DashboardConnectionString == null;

            if (_hasFastTableHook && noDashboardStorage)
            {
                return new FastTableLoggerProvider(_loggerFactory);
            }

            return new DefaultLoggerProvider(_storageAccountProvider, _loggerFactory);
        }

        public T GetLoggerProvider<T>() where T : class => _loggerProvider.Value as T;
    }
}
