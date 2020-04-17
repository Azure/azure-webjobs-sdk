// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    [Serializable]
    public class CacheObjectMetadata
    {
        private readonly string _etag;
        

        public CacheObjectMetadata(string uri, string name, string etag)
        {
            Uri = uri;
            Name = name;
            _etag = etag;
            // TODO TEMP, we need to retain the etag and check against it
            _etag = null;
        }
        
        public string Uri { get; private set; }

        public string Name { get; private set; }
        
        public string Container { get; private set; }
        
        // TODO what to do when etag is null?
        public override int GetHashCode()
        {
            if (_etag != null)
            {
                return Uri.GetHashCode() ^ _etag.GetHashCode();
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

            return other.Uri == this.Uri && other._etag == this._etag;
        }
    }
}
