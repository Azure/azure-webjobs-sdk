﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage.Table;

#if PUBLICSTORAGE
namespace Microsoft.Azure.WebJobs.Storage.Table
#else
namespace Microsoft.Azure.WebJobs.Host.Storage.Table
#endif
{
    /// <summary>Defines an operation on a table.</summary>
    public interface IStorageTableOperation
    {
        /// <summary>Gets the type of operation to perform.</summary>
        /// <remarks>
        /// When <see cref="OperationType"/> is <see cref="TableOperationType.Retrieve"/>, returns
        /// <see langword="null"/>.
        /// </remarks>
        TableOperationType OperationType { get; }

        /// <summary>Gets the entity on which to operate.</summary>
        /// <remarks>
        /// When <see cref="OperationType"/> is <see cref="TableOperationType.Retrieve"/>, returns
        /// <see langword="null"/>.
        /// </remarks>
        ITableEntity Entity { get; }

        /// <summary>Gets the partition key of the entity to retrieve.</summary>
        /// <remarks>
        /// When <see cref="OperationType"/> is not <see cref="TableOperationType.Retrieve"/>, returns
        /// <see langword="null"/>.
        /// </remarks>
        string RetrievePartitionKey { get; }

        /// <summary>Gets the row key of the entity to retrieve.</summary>
        /// <remarks>
        /// When <see cref="OperationType"/> is not <see cref="TableOperationType.Retrieve"/>, returns
        /// <see langword="null"/>.
        /// </remarks>
        string RetrieveRowKey { get; }

        /// <summary>Gets the resolver to resolve the entity to retrieve.</summary>
        /// <remarks>
        /// When <see cref="OperationType"/> is not <see cref="TableOperationType.Retrieve"/>, returns
        /// <see langword="null"/>.
        /// </remarks>
        IEntityResolver RetrieveEntityResolver { get; }
    }
}
