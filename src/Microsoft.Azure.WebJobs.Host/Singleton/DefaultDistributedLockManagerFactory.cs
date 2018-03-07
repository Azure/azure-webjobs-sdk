using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host
{
    public class DefaultDistributedLockManagerFactory : IDistributedLockManagerFactory
    {
        private readonly IOptions<JobHostInternalStorageOptions> _internalStorageOptions;
        private readonly IStorageAccountProvider _storageAccountProvider;
        private readonly ILoggerFactory _loggerFactory;

        public DefaultDistributedLockManagerFactory(IOptions<JobHostInternalStorageOptions> internalStorageOptions,
            IStorageAccountProvider storageAccountProvider,
            ILoggerFactory loggerFactory)
        {
            _internalStorageOptions = internalStorageOptions;
            _storageAccountProvider = storageAccountProvider;
            _loggerFactory = loggerFactory;
        }

        public IDistributedLockManager Create()
        {
            ILogger logger = _loggerFactory.CreateLogger<IDistributedLockManager>();
            var sas = _internalStorageOptions.Value;
            IDistributedLockManager lockManager;
            if (sas != null && sas.InternalContainer != null)
            {
                lockManager = new BlobLeaseDistributedLockManager.SasContainer(sas.InternalContainer, logger);
            }
            else
            {
                lockManager = new BlobLeaseDistributedLockManager.DedicatedStorage(_storageAccountProvider, logger);
            }

            return lockManager;
        }
    }
}
