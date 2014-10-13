// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class ValidateCachedHostIdProvider : IHostIdProvider
    {
        private readonly IHostIdProvider _innerProvider;
        private string _cachedHostId;
        private bool _isValueCached;

        public ValidateCachedHostIdProvider(IHostIdProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }

            _innerProvider = provider;
            _isValueCached = false;
        }

        /// <inheritdoc />
        public async Task<string> GetHostIdAsync(IEnumerable<MethodInfo> indexedMethods, CancellationToken cancellationToken)
        {
            if (!_isValueCached)
            {
                string hostId = await _innerProvider.GetHostIdAsync(indexedMethods, cancellationToken);

                if (!HostIdValidator.IsValid(hostId))
                {
                    throw new InvalidOperationException(HostIdValidator.ValidationMessage);
                }

                _cachedHostId = hostId;
                _isValueCached = true;
            }

            return _cachedHostId;
        }
    }
}
