﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Dashboard.Data
{
    public class RecentInvocationIndexByParentWriter : IRecentInvocationIndexByParentWriter
    {
        private readonly IConcurrentMetadataTextStore _store;

        [CLSCompliant(false)]
        public RecentInvocationIndexByParentWriter(CloudBlobClient client)
            : this(ConcurrentTextStore.CreateBlobStore(
                client, DashboardContainerNames.Dashboard, DashboardDirectoryNames.RecentFunctionsByParent))
        {
        }

        private RecentInvocationIndexByParentWriter(IConcurrentMetadataTextStore store)
        {
            _store = store;
        }

        public void CreateOrUpdate(FunctionInstanceSnapshot snapshot, DateTimeOffset timestamp)
        {
            var innerId = CreateInnerId(snapshot.ParentId.Value, timestamp, snapshot.Id);
            var metadata = FunctionInstanceMetadata.CreateFromSnapshot(snapshot);
            _store.CreateOrUpdate(innerId, metadata, String.Empty);
        }

        public void DeleteIfExists(Guid parentId, DateTimeOffset timestamp, Guid id)
        {
            string innerId = CreateInnerId(parentId, timestamp, id);
            _store.DeleteIfExists(innerId);
        }

        private static string CreateInnerId(Guid parentId, DateTimeOffset timestamp, Guid id)
        {
            return DashboardBlobPrefixes.CreateByParentRelativePrefix(parentId) +
                RecentInvocationEntry.Format(timestamp, id);
        }
    }
}
