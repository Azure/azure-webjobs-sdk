// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.ComponentModel;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    public enum CacheObjectType
    {
        Stream = 0,
        ByteArray = 1,
        Unknown = 2
    }

    public class CacheObjectMetadata
    {
        public CacheObjectMetadata(string uri, string name, string containerName, string etag, CacheObjectType cacheObjectType)
        {
            Uri = uri;
            Name = name;
            ContainerName = containerName;
            Etag = etag;
            CacheObjectType = cacheObjectType;

            // TODO TEMP, we need to retain the etag and check against it
            Etag = null;
        }
        
        public string Uri { get; private set; }

        public string Name { get; private set; }
        
        public string Etag { get; private set; }
        
        public string ContainerName { get; private set; }

        public CacheObjectType CacheObjectType { get; private set; }
        
        // TODO what to do when etag is null?
        public override int GetHashCode()
        {
            if (Etag != null)
            {
                return Uri.GetHashCode() ^ Etag.GetHashCode();
            }

            return Uri.GetHashCode();
        }
        
        // TODO what to do when etag is null?
        public override bool Equals(object obj)
        {
            if (!(obj is CacheObjectMetadata other))
            {
                return false;
            }

            return other.Uri == this.Uri && other.Etag == this.Etag;
        }
    }
}
