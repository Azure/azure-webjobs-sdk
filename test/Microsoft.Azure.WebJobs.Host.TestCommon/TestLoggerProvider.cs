// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public class TestLoggerProvider : ILoggerProvider
    {
        private readonly LoggerFilterOptions _filter;
        private readonly Action<LogMessage> _logAction;
        private Dictionary<string, TestLogger> _loggerCache { get; } = new Dictionary<string, TestLogger>();

        public TestLoggerProvider(Action<LogMessage> logAction = null)
        {
            _filter = new LoggerFilterOptions();
            _logAction = logAction;
        }

        public IList<TestLogger> CreatedLoggers => _loggerCache.Values.ToList();

        public ILogger CreateLogger(string categoryName)
        {
            if (!_loggerCache.TryGetValue(categoryName, out TestLogger logger))
            {
                logger = new TestLogger(categoryName, _logAction);
                _loggerCache.Add(categoryName, logger);
            }

            return logger;
        }

        public IEnumerable<LogMessage> GetAllLogMessages() => CreatedLoggers.SelectMany(l => l.GetLogMessages()).OrderBy(p => p.Timestamp);

        public string GetLogString() => string.Join(Environment.NewLine, GetAllLogMessages());

        public string GetTrimmedLogString(int maxLength = 4096)
        {
            string fullString = GetLogString();

            // return the last 4096, which is all Appveyor can handle.
            int trimmedLength = fullString.Length - maxLength;

            return trimmedLength <= 0 ? fullString : fullString.Substring(maxLength);
        }

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
