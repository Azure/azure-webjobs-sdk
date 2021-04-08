// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host
{
    // TODO: Support for multiple accounts?
    public interface ILeaseProvider
    {
        string GetLockId();

        Task CreateLeaseAsync(CancellationToken cancellationToken);

        Task RenewAsync(string leaseId, CancellationToken cancellationToken);

        Task<string> AcquireAsync(TimeSpan duration, CancellationToken cancellationToken, string leaseId = null);

        Task ReleaseAsync(string leaseId, CancellationToken cancellationToken);

        Task<string> GetLeaseOwner(CancellationToken cancellationToken);

        // Read first then write
        Task SetLeaseOwner(string owner, string leaseId, CancellationToken cancellationToken);
    }

    public interface ILeaseProviderFactory
    {
        ILeaseProvider GetLeaseProvider(string lockId, string accountOverride = null);
    }

    public class LeaseException : Exception
    {
        public int Status;
        public string ErrorCode;

        public LeaseException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class LeaseNotObtainedException : LeaseException
    {
        public LeaseNotObtainedException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class LeaseConflictException : LeaseException
    {
        public LeaseConflictException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class LeaseNotCreatedException : LeaseException
    {
        public LeaseNotCreatedException(string message, Exception innerException) : base(message, innerException) { }
    }
}
