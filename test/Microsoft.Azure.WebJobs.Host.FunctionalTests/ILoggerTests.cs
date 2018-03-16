﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.FunctionalTests.TestDoubles;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    public class ILoggerTests
    {
        TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        [Fact]
        public void ILogger_Succeeds()
        {
            string functionName = nameof(ILoggerFunctions.ILogger);
            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(CreateConfig()), new Mock<IJobHostContextFactory>().Object))
            {
                var method = typeof(ILoggerFunctions).GetMethod(functionName);
                host.Call(method);
            }

            // Six loggers are the startup, singleton, results, function and function.user
            Assert.Equal(5, _loggerProvider.CreatedLoggers.Count());

            var functionLogger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.CreateFunctionUserCategory(functionName)).Single();
            var resultsLogger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Results).Single();

            var messages = functionLogger.GetLogMessages();
            Assert.Equal(2, messages.Count);
            var infoMessage = messages[0];
            var errorMessage = messages[1];

            // These get the {OriginalFormat} property as well as the 2 from structured log properties
            Assert.Equal(3, infoMessage.State.Count());
            Assert.Equal(3, errorMessage.State.Count());

            Assert.Equal(1, resultsLogger.GetLogMessages().Count);

            // TODO: beef these verifications up
        }

        [Fact]
        public void TraceWriter_ForwardsTo_ILogger()
        {
            string functionName = nameof(ILoggerFunctions.TraceWriterWithILoggerFactory);
            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(CreateConfig()), new Mock<IJobHostContextFactory>().Object))
            {
                var method = typeof(ILoggerFunctions).GetMethod(functionName);
                host.Call(method);
            }

            // Five loggers are the startup, singleton, results, function and function.user
            Assert.Equal(5, _loggerProvider.CreatedLoggers.Count());
            var functionLogger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.CreateFunctionUserCategory(functionName)).Single();
            var messages = functionLogger.GetLogMessages();
            Assert.Equal(2, messages.Count);
            var infoMessage = messages[0];
            var errorMessage = messages[1];

            // These get the {OriginalFormat} only
            Assert.Single(infoMessage.State);
            Assert.Single(errorMessage.State);

            //TODO: beef these verifications up
        }

        [Fact]
        public void Aggregator_Runs_WhenEnabled_AndFlushes_OnStop()
        {
            int addCalls = 0;
            int flushCalls = 0;

            var config = CreateConfig();

            var mockAggregator = new Mock<IAsyncCollector<FunctionInstanceLogEntry>>(MockBehavior.Strict);
            mockAggregator
                .Setup(a => a.AddAsync(It.IsAny<FunctionInstanceLogEntry>(), It.IsAny<CancellationToken>()))
                .Callback<FunctionInstanceLogEntry, CancellationToken>((l, t) =>
                {
                    if (l.IsCompleted)
                    {
                        addCalls++; // The default aggregator will ingore the 'Function started' calls.
                    }
                })
                .Returns(Task.CompletedTask);
            mockAggregator
                .Setup(a => a.FlushAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(t => flushCalls++)
                .Returns(Task.CompletedTask);

            var mockFactory = new Mock<IFunctionResultAggregatorFactory>(MockBehavior.Strict);
            mockFactory
                .Setup(f => f.Create(5, TimeSpan.FromSeconds(1), It.IsAny<ILoggerFactory>()))
                .Returns(mockAggregator.Object);

            config.AddService<IFunctionResultAggregatorFactory>(mockFactory.Object);

            const int N = 5;
            // TODO: DI:
            //config.Aggregator.IsEnabled = true;
            //config.Aggregator.BatchSize = N;
            //config.Aggregator.FlushTimeout = TimeSpan.FromSeconds(1);

            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(CreateConfig()), new Mock<IJobHostContextFactory>().Object))
            {
                host.Start();

                var method = typeof(ILoggerFunctions).GetMethod(nameof(ILoggerFunctions.TraceWriterWithILoggerFactory));

                for (int i = 0; i < N; i++)
                {
                    host.Call(method);
                }

                host.Stop();
            }

            Assert.Equal(N, addCalls);

            // Flush is called on host stop
            Assert.Equal(1, flushCalls);
        }

        [Fact]
        public void NoILoggerFactory_NoAggregator()
        {
            var config = CreateConfig(addFactory: false);

            // Ensure the aggregator is never configured by registering an
            // AggregatorFactory that with a strict, unconfigured mock.
            var mockFactory = new Mock<IFunctionResultAggregatorFactory>(MockBehavior.Strict);
            config.AddService<IFunctionResultAggregatorFactory>(mockFactory.Object);

            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(config), new Mock<IJobHostContextFactory>().Object))
            {
                var method = typeof(ILoggerFunctions).GetMethod(nameof(ILoggerFunctions.TraceWriterWithILoggerFactory));
                host.Call(method);
            }
        }

        [Fact]
        public void DisabledAggregator_NoAggregator()
        {
            // Add the loggerfactory but disable the aggregator
            var config = CreateConfig();
            // TODO: DI:
            //config.Aggregator.IsEnabled = false;

            // Ensure the aggregator is never configured by registering an
            // AggregatorFactory that with a strict, unconfigured mock.
            var mockFactory = new Mock<IFunctionResultAggregatorFactory>(MockBehavior.Strict);
            config.AddService<IFunctionResultAggregatorFactory>(mockFactory.Object);

            using (JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(config), new Mock<IJobHostContextFactory>().Object))
            {
                // also start and stop the host to ensure nothing throws due to the
                // null aggregator
                host.Start();

                var method = typeof(ILoggerFunctions).GetMethod(nameof(ILoggerFunctions.TraceWriterWithILoggerFactory));
                host.Call(method);

                host.Stop();
            }
        }

        private JobHostOptions CreateConfig(bool addFactory = true)
        {
            IStorageAccountProvider accountProvider = new FakeStorageAccountProvider()
            {
                StorageAccount = new FakeStorageAccount()
            };

            ILoggerFactory factory = new LoggerFactory();
            factory.AddProvider(_loggerProvider);

            var config = new JobHostOptions();
            config.AddService(accountProvider);
            // TODO: DI: 
            //config.TypeLocator = new FakeTypeLocator(new[] { typeof(ILoggerFunctions) });
            config.AddService(factory);
            //config.Aggregator.IsEnabled = false; // disable aggregator

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
    }
}
