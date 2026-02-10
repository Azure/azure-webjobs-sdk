// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Hosting
{
    /// <summary>
    /// A background service that drains buffered options log messages from an <see cref="IOptionsLoggingSource"/>
    /// and writes them to an <see cref="ILogger"/>. This service is part of the options logging infrastructure
    /// registered by <see cref="WebJobsServiceCollectionExtensions.AddOptionsLogging"/>.
    /// </summary>
    public sealed class OptionsLoggingService : IHostedService
    {
        private readonly ILogger<OptionsLoggingService> _logger;
        private readonly IOptionsLoggingSource _source;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _processingTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionsLoggingService"/> class.
        /// </summary>
        /// <param name="source">The source to consume log messages from.</param>
        /// <param name="logger">The logger to write options logs to.</param>
        public OptionsLoggingService(IOptionsLoggingSource source, ILogger<OptionsLoggingService> logger)
        {
            _logger = logger;
            _source = source;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _processingTask = ProcessLogs();
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();
            await _processingTask;
        }

        private async Task ProcessLogs()
        {
            ISourceBlock<string> source = _source.LogStream;
            try
            {
                while (await source.OutputAvailableAsync(_cts.Token))
                {
                    _logger.LogInformation(await source.ReceiveAsync());
                }
            }
            catch (OperationCanceledException)
            {
                // This occurs during shutdown.
            }
        }
    }
}
