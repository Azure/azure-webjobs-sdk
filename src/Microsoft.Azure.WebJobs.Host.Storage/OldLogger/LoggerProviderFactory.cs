// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal sealed class LoggerProviderFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly bool _hasFastTableHook;
        private readonly Lazy<object> _loggerProvider;
        private readonly LegacyConfig _storageAccountProvider;

        public LoggerProviderFactory(
            IOptions<LegacyConfig> storageAccountProvider,
            ILoggerFactory loggerFactory,
            IEventCollectorFactory fastLoggerFactory = null)
        {
            _storageAccountProvider = storageAccountProvider.Value;
            _loggerFactory = loggerFactory;
            _hasFastTableHook = fastLoggerFactory != null;

            _loggerProvider = new Lazy<object>(CreateLoggerProvider);
        }

        private object CreateLoggerProvider()
        {
            bool noDashboardStorage = _storageAccountProvider.Dashboard == null; // $$$ if this is null, we should have registered different DI components
            
            if (_hasFastTableHook && noDashboardStorage)
            {
                return new FastTableLoggerProvider(_loggerFactory);
            }

            return new DefaultLoggerProvider(_storageAccountProvider, _loggerFactory);
        }

        public T GetLoggerProvider<T>() where T : class => _loggerProvider.Value as T;
    }
}
