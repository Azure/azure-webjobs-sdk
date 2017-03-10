using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.FunctionalTests.TestDoubles;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    public class ILoggerTests
    {
        TestTraceWriter _trace = new TestTraceWriter(TraceLevel.Info);
        TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        [Fact]
        public void ILogger_Succeeds()
        {
            using (JobHost host = new JobHost(CreateConfig()))
            {
                var method = typeof(ILoggerFunctions).GetMethod(nameof(ILoggerFunctions.ILogger));
                host.Call(method);
            }

            // Three loggers are the executor, results, and function loggers
            Assert.Equal(3, _loggerProvider.CreatedLoggers.Count);

            var functionLogger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LoggingCategories.Function).Single();
            var resultsLogger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LoggingCategories.Results).Single();

            Assert.Equal(2, functionLogger.LogMessages.Count);
            var infoMessage = functionLogger.LogMessages[0];
            var errorMessage = functionLogger.LogMessages[1];
            // These get the {OriginalFormat} property as well as the 3 from TraceWriter
            Assert.Equal(3, infoMessage.State.Count());
            Assert.Equal(3, errorMessage.State.Count());

            Assert.Equal(1, resultsLogger.LogMessages.Count);
            //TODO: beef these verifications up
        }

        [Fact]
        public void TraceWriter_ForwardsTo_ILogger()
        {
            using (JobHost host = new JobHost(CreateConfig()))
            {
                var method = typeof(ILoggerFunctions).GetMethod(nameof(ILoggerFunctions.TraceWriterWithILoggerFactory));
                host.Call(method);
            }

            Assert.Equal(5, _trace.Traces.Count);
            // The third and fourth traces are from our function
            var infoLog = _trace.Traces[2];
            var errorLog = _trace.Traces[3];

            Assert.Equal("This should go to the ILogger", infoLog.Message);
            Assert.Null(infoLog.Exception);
            Assert.Equal(3, infoLog.Properties.Count);

            Assert.Equal("This should go to the ILogger with an Exception!", errorLog.Message);
            Assert.IsType<InvalidOperationException>(errorLog.Exception);
            Assert.Equal(3, errorLog.Properties.Count);

            // Three loggers are the executor, results, and function loggers
            Assert.Equal(3, _loggerProvider.CreatedLoggers.Count);
            var functionLogger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LoggingCategories.Function).Single();
            Assert.Equal(2, functionLogger.LogMessages.Count);
            var infoMessage = functionLogger.LogMessages[0];
            var errorMessage = functionLogger.LogMessages[1];
            // These get the {OriginalFormat} property as well as the 3 from TraceWriter
            Assert.Equal(4, infoMessage.State.Count());
            Assert.Equal(4, errorMessage.State.Count());
            //TODO: beef these verifications up
        }

        private JobHostConfiguration CreateConfig()
        {
            IStorageAccountProvider accountProvider = new FakeStorageAccountProvider()
            {
                StorageAccount = new FakeStorageAccount()
            };

            ILoggerFactory factory = new LoggerFactory();
            factory.AddProvider(_loggerProvider);

            var config = new JobHostConfiguration();
            config.AddService(accountProvider);
            config.TypeLocator = new FakeTypeLocator(new[] { typeof(ILoggerFunctions) });
            config.Tracing.Tracers.Add(_trace);
            config.AddService(factory);
            config.Aggregator.IsEnabled = false; // turn off aggregation

            return config;
        }

        private class ILoggerFunctions
        {
            [NoAutomaticTrigger]
            public void ILogger(ILogger log)
            {
                log.LogInformation("Log {some} keys and {values}", "1", "2");

                var ex = new InvalidOperationException("Failure.");
                log.LogError(0, ex, "Log {other} keys {and} values", "3", "4");
            }

            [NoAutomaticTrigger]
            public void TraceWriterWithILoggerFactory(TraceWriter log)
            {
                log.Info("This should go to the ILogger");

                var ex = new InvalidOperationException("Failure.");
                log.Error("This should go to the ILogger with an Exception!", ex);
            }
        }

        private class TestLoggerProvider : ILoggerProvider
        {
            public IList<TestLogger> CreatedLoggers = new List<TestLogger>();

            public ILogger CreateLogger(string categoryName)
            {
                var logger = new TestLogger(categoryName);
                CreatedLoggers.Add(logger);
                return logger;
            }

            public void Dispose()
            {
            }
        }

        private class TestLogger : ILogger
        {
            public string Category { get; private set; }

            public IList<LogMessage> LogMessages = new List<LogMessage>();

            public TestLogger(string category)
            {
                Category = category;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                throw new NotImplementedException();
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                LogMessages.Add(new LogMessage
                {
                    Level = logLevel,
                    EventId = eventId,
                    State = state as IEnumerable<KeyValuePair<string, object>>,
                    Exception = exception,
                    FormattedMessage = formatter(state, exception)
                });
            }
        }

        private class LogMessage
        {
            public LogLevel Level { get; set; }
            public EventId EventId { get; set; }
            public IEnumerable<KeyValuePair<string, object>> State { get; set; }
            public Exception Exception { get; set; }
            public string FormattedMessage { get; set; }
        }
    }
}
