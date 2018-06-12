﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public class TestLoggerProvider : ILoggerProvider
    {
        private readonly Func<string, LogLevel, bool> _filter;
        private readonly Action<LogMessage> _logAction;
        private Dictionary<string, TestLogger> _loggerCache { get; } = new Dictionary<string, TestLogger>();

        public TestLoggerProvider(Func<string, LogLevel, bool> filter = null, Action<LogMessage> logAction = null)
        {
            _filter = filter ?? new LogCategoryFilter().Filter;
            _logAction = logAction;
        }

        public IList<TestLogger> CreatedLoggers => _loggerCache.Values.ToList();

        public ILogger CreateLogger(string categoryName)
        {
            if (!_loggerCache.TryGetValue(categoryName, out TestLogger logger))
            {
                logger = new TestLogger(categoryName, _filter, _logAction);
                _loggerCache.Add(categoryName, logger);
            }

            return logger;
        }

        public IEnumerable<LogMessage> GetAllLogMessages() => CreatedLoggers.SelectMany(l => l.GetLogMessages()).OrderBy(p => p.Timestamp);

        public void ClearAllLogMessages()
        {
            foreach (TestLogger logger in CreatedLoggers)
            {
                logger.ClearLogMessages();
            }
        }

        public void Dispose()
        {
        }
    }
}
