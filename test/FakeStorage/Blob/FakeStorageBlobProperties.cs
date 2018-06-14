﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage.Blob;

namespace FakeStorage
{
    internal class FakeStorageBlobProperties
    {
        public string ETag { get; set; }

        public DateTimeOffset? LastModified { get; set; }

        public LeaseState LeaseState { get; set; }

        public LeaseStatus LeaseStatus { get; set; }

        public BlobProperties SdkObject { get; set; }
    }
}
