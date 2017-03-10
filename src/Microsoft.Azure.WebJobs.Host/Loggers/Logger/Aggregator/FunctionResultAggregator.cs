// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal class FunctionResultAggregator : IFunctionResultAggregator, IDisposable
    {
        private readonly ILogger _logger;
        private readonly int _batchSize;
        private readonly TimeSpan _batchTimeout;

        private BufferBlock<FunctionResultLog> _buffer;
        private BatchBlock<FunctionResultLog> _batcher;

        private Timer _windowTimer;
        private IDisposable[] _disposables;

        public FunctionResultAggregator(int batchSize, TimeSpan batchTimeout, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(LoggingCategories.Aggregator);
            _batchSize = batchSize;
            _batchTimeout = batchTimeout;
            InitializeFlow(_batchSize, _batchTimeout);
        }

        private void InitializeFlow(int maxBacklog, TimeSpan maxFlushInterval)
        {
            _buffer = new BufferBlock<FunctionResultLog>(
                new ExecutionDataflowBlockOptions()
                {
                    BoundedCapacity = maxBacklog
                });

            _batcher = new BatchBlock<FunctionResultLog>(maxBacklog,
                new GroupingDataflowBlockOptions()
                {
                    BoundedCapacity = maxBacklog,
                    Greedy = true
                });

            TransformBlock<IEnumerable<FunctionResultLog>, IEnumerable<FunctionResultAggregate>> aggregator =
                new TransformBlock<IEnumerable<FunctionResultLog>, IEnumerable<FunctionResultAggregate>>(transform: (e) => Aggregate(e));

            ActionBlock<IEnumerable<FunctionResultAggregate>> publisher = new ActionBlock<IEnumerable<FunctionResultAggregate>>(
                (e) => Publish(e),
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = 1,
                    BoundedCapacity = 32
                });

            _disposables = new IDisposable[]
            {
                _buffer.LinkTo(_batcher),
                _batcher.LinkTo(aggregator),
                aggregator.LinkTo(publisher)
            };

            _windowTimer = new Timer(async (o) => await FlushAsync(), null, maxFlushInterval, maxFlushInterval);
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            _batcher?.TriggerBatch();
            return Task.FromResult(0);
        }

        internal void Publish(IEnumerable<FunctionResultAggregate> results)
        {
            foreach (var result in results)
            {
                _logger.LogFunctionResultAggregate(result);
            }
        }

        public async Task AddAsync(FunctionResultLog result, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _buffer.SendAsync(result, cancellationToken);
        }

        internal static IEnumerable<FunctionResultAggregate> Aggregate(IEnumerable<FunctionResultLog> evts)
        {
            var metrics = evts
                .GroupBy(e => new { e.Name })
                .Select(e => new FunctionResultAggregate
                {
                    Name = e.Key.Name,
                    Timestamp = e.Min(t => t.StartTime),
                    Successes = e.Count(f => f.Success),
                    Failures = e.Count(f => !f.Success),
                    MinMilliseconds = e.Min(f => f.DurationInMilliseconds),
                    MaxMilliseconds = e.Max(f => f.DurationInMilliseconds),
                    AverageMilliseconds = (int)e.Average(f => f.DurationInMilliseconds)
                });

            return metrics;
        }

        public void Dispose()
        {
            if (_disposables != null)
            {
                foreach (var d in _disposables)
                {
                    d.Dispose();
                }
                _disposables = null;
            }

            if (_windowTimer != null)
            {
                _windowTimer.Dispose();
                _windowTimer = null;
            }
        }
    }
}
