// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    public class ILoggerTests
    {
        [Fact]
        public void ILogger_Succeeds()
        {
            string functionName = nameof(ILoggerFunctions.ILogger);
            IHost host = ConfigureHostBuilder().Build();
            var loggerProvider = host.GetTestLoggerProvider();

            using (host)
            {
                var method = typeof(ILoggerFunctions).GetMethod(functionName);
                host.GetJobHost().Call(method);
            }

            // Six loggers are the startup, singleton, results, function and function.user
            // Note: We currently have 3 additional Logger<T> categories that need to be renamed
            Assert.Equal(7, loggerProvider.CreatedLoggers.Count); // $$$ was 9?

            var functionLogger = loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.CreateFunctionUserCategory(functionName)).Single();
            var resultsLogger = loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Results).Single();

            Assert.Equal(2, functionLogger.GetLogMessages().Count);
            var infoMessage = functionLogger.GetLogMessages()[0];
            var errorMessage = functionLogger.GetLogMessages()[1];

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

            IHost host = ConfigureHostBuilder().Build();
            var loggerProvider = host.GetTestLoggerProvider();

            using (host)
            {
                var method = typeof(ILoggerFunctions).GetMethod(functionName);
                host.GetJobHost().Call(method);
            }

            // Five loggers are the startup, singleton, results, function and function.user
            Assert.Equal(7, loggerProvider.CreatedLoggers.Count); // $$$ was 9? 
            var functionLogger = loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.CreateFunctionUserCategory(functionName)).Single();
            Assert.Equal(2, functionLogger.GetLogMessages().Count);
            var infoMessage = functionLogger.GetLogMessages()[0];
            var errorMessage = functionLogger.GetLogMessages()[1];

            // These get the {OriginalFormat} only
            Assert.Single(infoMessage.State);
            Assert.Single(errorMessage.State);

            //TODO: beef these verifications up
        }

        [Fact]
        public async Task Aggregator_Runs_WhenEnabled_AndFlushes_OnStop()
        {
            int addCalls = 0;
            int flushCalls = 0;

            var config = ConfigureHostBuilder();

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

            const int N = 5;

            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ILoggerFunctions>(b =>
                {
                    b.AddAzureStorage();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IAsyncCollector<FunctionInstanceLogEntry>>(mockAggregator.Object);
                    services.Configure<FunctionResultAggregatorOptions>(o =>
                    {
                        o.IsEnabled = true;
                        o.BatchSize = N;
                        o.FlushTimeout = TimeSpan.FromSeconds(1);
                    });
                })
                .Build();

            using (host)
            {
                host.Start();

                var method = typeof(ILoggerFunctions).GetMethod(nameof(ILoggerFunctions.TraceWriterWithILoggerFactory));

                for (int i = 0; i < N; i++)
                {
                    host.GetJobHost().Call(method);
                }

                await host.StopAsync();
            }

            Assert.Equal(N, addCalls);

            // Flush is called on host stop
            Assert.Equal(1, flushCalls);
        }

        [Fact]
        public async Task DisabledAggregator_NoAggregator()
        {
            // Ensure our default aggregator returns null when the aggregator is disabled.
            var hostBuilder = ConfigureHostBuilder()
                .ConfigureServices(services =>
                {
                    // TODO: Is there a better way to register these? This is the only way to remove
                    //       the default-registered Aggregator, which seems unintuitive.
                    services.RemoveAll<IEventCollectorProvider>();
                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventCollectorProvider, MockAggregatorProvider>());

                    // register a validator to make sure the returned value is null.
                    services.AddSingleton<Action<IAsyncCollector<FunctionInstanceLogEntry>>>(r =>
                    {
                        Assert.Null(r);
                    });
                });

            using (IHost host = hostBuilder.Build())
            {
                // also start and stop the host to ensure nothing throws due to the
                // null aggregator
                host.Start();

                var method = typeof(ILoggerFunctions).GetMethod(nameof(ILoggerFunctions.TraceWriterWithILoggerFactory));
                host.GetJobHost().Call(method);

                await host.StopAsync();
            }
        }

        private IHostBuilder ConfigureHostBuilder()
        {
            return new HostBuilder()
                .ConfigureDefaultTestHost<ILoggerFunctions>()
                .ConfigureServices(services =>
                {
                    services.Configure<FunctionResultAggregatorOptions>(o => o.IsEnabled = false);
                });
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

        private class MockAggregatorProvider : FunctionResultAggregatorProvider
        {
            private readonly Action<IAsyncCollector<FunctionInstanceLogEntry>> _validateCallback;

            public MockAggregatorProvider(Action<IAsyncCollector<FunctionInstanceLogEntry>> validateCallback, IOptions<FunctionResultAggregatorOptions> options, ILoggerFactory loggerFactory) :
                base(options, loggerFactory)
            {
                _validateCallback = validateCallback;
            }

            public override IAsyncCollector<FunctionInstanceLogEntry> Create()
            {
                var collector = base.Create();
                _validateCallback(collector);
                return collector;
            }
        }
    }
}
