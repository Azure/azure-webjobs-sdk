using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Hosting
{
    public class JobHostService : IHostedService
    {
        private readonly ILogger<JobHostService> _logger;
        private readonly IJobHost _jobHost;

        public JobHostService(IJobHost jobhost, ILogger<JobHostService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jobHost = jobhost;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting JobHost");
            return _jobHost.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping JobHost");
            return _jobHost.StopAsync();
        }
    }
}
