﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public class TestLogger : ILogger
    {
        private readonly Func<string, LogLevel, bool> _filter;
        private Action<LogMessage> _logAction;
        private IList<LogMessage> _logMessages = new List<LogMessage>();

        public string Category { get; private set; }

        public TestLogger(string category, Func<string, LogLevel, bool> filter = null, Action<LogMessage> logAction = null)
        {
            Category = category;
            _filter = filter;
            _logAction = logAction;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _filter?.Invoke(Category, logLevel) ?? true;
        }

        public IList<LogMessage> GetLogMessages() => _logMessages.ToList();

        public void ClearLogMessages() => _logMessages.Clear();

        public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            LogMessage logMessage = new LogMessage
            {
                Level = logLevel,
                EventId = eventId,
                State = state as IEnumerable<KeyValuePair<string, object>>,
                Exception = exception,
                FormattedMessage = formatter(state, exception),
                Category = Category
            };

            _logMessages.Add(logMessage);
            _logAction?.Invoke(logMessage);
        }

        public override string ToString()
        {
            return Category;
        }
    }
}