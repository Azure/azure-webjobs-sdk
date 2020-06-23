// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Implementation of a provider that used to create <see cref="IRetryManager"/>
    /// </summary>
    internal class DefautRetryManagerProvider : IRetryManagerProvider
    {
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        ///  Constructs a new instance.
        /// </summary>
        /// <param name="loggerFactory"></param>
        public DefautRetryManagerProvider(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="retryAttribute">Retry attribute.</param>
        /// <returns>Return <see cref="IRetryManager"/></returns>
        public IRetryManager Create(RetryAttribute retryAttribute)
        {
            return new DefaultRetryManager(retryAttribute, _loggerFactory);
        }
    }
}
