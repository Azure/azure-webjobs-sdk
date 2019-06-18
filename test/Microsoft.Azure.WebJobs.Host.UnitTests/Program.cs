// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
        }
    }

    [MemoryDiagnoser]
    [InProcess]
    public class Logging
    {
        FunctionInstanceLogger _nullLogger;
        FunctionInstanceLogger _formattingLogger;
        FunctionCompletedMessage _message;

        [GlobalSetup]
        public void Setup()
        {
            _nullLogger = new FunctionInstanceLogger(new NullLoggerFactory());
            _formattingLogger = new FunctionInstanceLogger(new FormattingLoggerFactory());

            _message = new FunctionCompletedMessage
            {
                Function = new FunctionDescriptor
                {
                    ShortName = "TestJob",
                    LogName = "TestJob",
                    FullName = "Functions.TestJob"
                },
                ReasonDetails = "reason",
                HostInstanceId = Guid.NewGuid(),
                FunctionInstanceId = Guid.NewGuid(),
                TriggerDetails = new Dictionary<string, string>()
                {
                    {"MessageId", Guid.NewGuid().ToString()},
                    {"DequeueCount", "1"},
                    {"InsertionTime", DateTime.Now.ToString()}
                }
            };
        }

        [Benchmark]
        public Task NullLogger_LogFunctionStarted()
        {
            return _nullLogger.LogFunctionStartedAsync(_message, CancellationToken.None);
        }

        [Benchmark]
        public Task NullLogger_LogFunctionCompleted()
        {
            return _nullLogger.LogFunctionCompletedAsync(_message, CancellationToken.None);
        }

        [Benchmark]
        public Task FormattingLogger_LogFunctionStarted()
        {
            return _formattingLogger.LogFunctionStartedAsync(_message, CancellationToken.None);
        }

        [Benchmark]
        public Task FormattingLogger_LogFunctionCompleted()
        {
            return _formattingLogger.LogFunctionCompletedAsync(_message, CancellationToken.None);
        }

        class FormattingLoggerFactory : ILoggerFactory
        {
            public ILogger CreateLogger(string categoryName)
            {
                return new Logger();
            }

            public void Dispose()
            {
            }

            public void AddProvider(ILoggerProvider provider)
            {
            }

            class Logger : ILogger
            {
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                    GC.KeepAlive(formatter(state, exception));
                }

                public bool IsEnabled(LogLevel logLevel) => true;
                public IDisposable BeginScope<TState>(TState state) => null;
            }
        }
    }
}