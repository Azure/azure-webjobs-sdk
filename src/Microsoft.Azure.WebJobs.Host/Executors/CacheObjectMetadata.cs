// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    [Serializable]
    public class CacheObjectMetadata
    {
        private readonly string _uri;
        private readonly string _etag;
        

        public CacheObjectMetadata(string uri, string name, string etag)
        {
            _uri = uri;
            _etag = etag;
            Name = name;
            // TODO TEMP
            _etag = null;
        }
        
        public string Name { get; private set; }
        
        public string Container { get; private set; }
        
        // TODO what to do when etag is null?
        public override int GetHashCode()
        {
            if (_etag != null)
            {
                return _uri.GetHashCode() ^ _etag.GetHashCode();
            }

            return _uri.GetHashCode();
        }
        
        // TODO what to do when etag is null?
        public override bool Equals(object obj)
        {
            if (!(obj is CacheObjectMetadata other))
            {
                return false;
            }

            return other._uri == this._uri && other._etag == this._etag;
        }
    }
}
