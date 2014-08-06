﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Jobs.Protocols;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Dashboard.Data
{
    public class RecentInvocationIndexByJobRunWriter : IRecentInvocationIndexByJobRunWriter
    {
        private readonly IConcurrentMetadataTextStore _store;

        [CLSCompliant(false)]
        public RecentInvocationIndexByJobRunWriter(CloudBlobClient client)
            : this(ConcurrentTextStore.CreateBlobStore(
                client, DashboardContainerNames.Dashboard, DashboardDirectoryNames.RecentFunctionsByJobRun))
        {
        }

        private RecentInvocationIndexByJobRunWriter(IConcurrentMetadataTextStore store)
        {
            _store = store;
        }

        public void CreateOrUpdate(FunctionInstanceSnapshot snapshot, WebJobRunIdentifier webJobRunId, DateTimeOffset timestamp)
        {
            var innerId = CreateInnerId(webJobRunId, timestamp, snapshot.Id);
            var metadata = FunctionInstanceMetadata.CreateFromSnapshot(snapshot);
            _store.CreateOrUpdate(innerId, metadata, String.Empty);
        }

        public void DeleteIfExists(WebJobRunIdentifier webJobRunId, DateTimeOffset timestamp, Guid id)
        {
            string innerId = CreateInnerId(webJobRunId, timestamp, id);
            _store.DeleteIfExists(innerId);
        }

        private static string CreateInnerId(WebJobRunIdentifier webJobRunId, DateTimeOffset timestamp, Guid id)
        {
            return DashboardBlobPrefixes.CreateByJobRunRelativePrefix(webJobRunId) +
                RecentInvocationEntry.Format(timestamp, id);
        }
    }
}
