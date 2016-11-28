// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using Microsoft.WindowsAzure.Storage;

#if PUBLICSTORAGE
namespace Microsoft.Azure.WebJobs.Storage
#else
namespace Microsoft.Azure.WebJobs.Host.Storage
#endif
{

    /// <summary>Provides extension methods for the <see cref="StorageException"/> class.</summary>
#if PUBLICSTORAGE
    [CLSCompliant(false)]
    public static class StorageExceptionExtensions
#else
    internal static class StorageExceptionExtensions
#endif
    {
        /// <summary>
        /// Determines whether the exception is due to a 400 Bad Request error with the error code PopReceiptMismatch.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 400 Bad Request error with the error code
        /// PopReceiptMismatch; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsBadRequestPopReceiptMismatch(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.BadRequest;
        }

        /// <summary>Determines whether the exception is due to a 409 Conflict error.</summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 409 Conflict error; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsConflict(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.Conflict;
        }

        /// <summary>
        /// Determines whether the exception is due to a 409 Conflict error with the error code BlobAlreadyExists.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 409 Conflict error with the error code
        /// BlobAlreadyExists; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsConflictBlobAlreadyExists(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.Conflict;
        }

        /// <summary>
        /// Determines whether the exception is due to a 409 Conflict error with the error code LeaseAlreadyPresent.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 409 Conflict error with the error code
        /// LeaseAlreadyPresent; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsConflictLeaseAlreadyPresent(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.Conflict;
        }

        /// <summary>
        /// Determines whether the exception is due to a 409 Conflict error with the error code
        /// LeaseIdMismatchWithLeaseOperation.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 409 Conflict error with the error code
        /// LeaseIdMismatchWithLeaseOperation; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsConflictLeaseIdMismatchWithLeaseOperation(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.Conflict;
        }

        /// <summary>
        /// Determines whether the exception is due to a 409 Conflict error with the error code QueueBeingDeleted.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 409 Conflict error with the error code
        /// QueueBeingDeleted; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsConflictQueueBeingDeleted(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.Conflict;
        }

        /// <summary>
        /// Determines whether the exception is due to a 409 Conflict error with the error code QueueBeingDeleted or
        /// QueueDisabled.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 409 Conflict error with the error code QueueBeingDeleted
        /// or QueueDisabled; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsConflictQueueBeingDeletedOrDisabled(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.Conflict;
        }

        /// <summary>
        /// Determines whether the exception is due to a 409 Conflict error with the error code QueueDisabled.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 409 Conflict error with the error code QueueDisabled;
        /// otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsConflictQueueDisabled(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.Conflict;
        }

        /// <summary>Determines whether the exception is due to a 404 Not Found error.</summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 404 Not Found error; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsNotFound(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.NotFound;
        }

        /// <summary>
        /// Determines whether the exception is due to a 404 Not Found error with the error code BlobNotFound.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 404 Not Found error with the error code BlobNotFound;
        /// otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsNotFoundBlobNotFound(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.NotFound;
        }

        /// <summary>
        /// Determines whether the exception is due to a 404 Not Found error with the error code ContainerNotFound.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 404 Not Found error with the error code
        /// ContainerNotFound; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsNotFoundContainerNotFound(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.NotFound;
        }

        /// <summary>
        /// Determines whether the exception is due to a 404 Not Found error with the error code TableNotFound.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 404 Not Found error with the error code
        /// TableNotFound; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsNotFoundTableNotFound(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.NotFound;
        }

        /// <summary>
        /// Determines whether the exception is due to a 404 Not Found error with the error code BlobNotFound or
        /// ContainerNotFound.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 404 Not Found error with the error code BlobNotFound or
        /// ContainerNotFound; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsNotFoundBlobOrContainerNotFound(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.NotFound;
        }

        /// <summary>
        /// Determines whether the exception is due to a 404 Not Found error with the error code MessageNotFound or
        /// QueueNotFound.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 404 Not Found error with the error code MessageNotFound
        /// or QueueNotFound; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsNotFoundMessageOrQueueNotFound(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.NotFound;
        }

        /// <summary>
        /// Determines whether the exception is due to a 404 Not Found error with the error code MessageNotFound.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 404 Not Found error with the error code MessageNotFound;
        /// otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsNotFoundMessageNotFound(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.NotFound;
        }

        /// <summary>
        /// Determines whether the exception is due to a 404 Not Found error with the error code QueueNotFound.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 404 Not Found error with the error code QueueNotFound;
        /// otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsNotFoundQueueNotFound(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.NotFound;
        }

        /// <summary>Determines whether the exception occurred despite a 200 OK response.</summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception occurred despite a 200 OK response; otherwise
        /// <see langword="false"/>.
        /// </returns>
        public static bool IsOk(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.OK;
        }

        /// <summary>Determines whether the exception is due to a 412 Precondition Failed error.</summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 412 Precondition Failed error; otherwise
        /// <see langword="false"/>.
        /// </returns>
        public static bool IsPreconditionFailed(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed;
        }

        /// <summary>
        /// Determines whether the exception is due to a 412 Precondition Failed error with the error code
        /// ConditionNotMet.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 412 Precondition Failed error with the error code
        /// ConditionNotMet; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsPreconditionFailedConditionNotMet(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed;
        }

        /// <summary>
        /// Determines whether the exception is due to a 412 Precondition Failed error with the error code
        /// LeaseIdMissing.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 412 Precondition Failed error with the error code
        /// LeaseIdMissing; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsPreconditionFailedLeaseIdMissing(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed;
        }

        /// <summary>
        /// Determines whether the exception is due to a 412 Precondition Failed error with the error code LeaseLost.
        /// </summary>
        /// <param name="exception">The storage exception.</param>
        /// <returns>
        /// <see langword="true"/> if the exception is due to a 412 Precondition Failed error with the error code
        /// LeaseLost; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsPreconditionFailedLeaseLost(this StorageException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            RequestResult result = exception.RequestInformation;

            if (result == null)
            {
                return false;
            }

            return result.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed;
        }
    }
}
