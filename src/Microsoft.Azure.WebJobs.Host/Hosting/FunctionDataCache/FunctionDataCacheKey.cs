// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs
{
    public class FunctionDataCacheKey
    {
        public FunctionDataCacheKey(string id, string version)
        {
            Id = id;
            Version = version;
        }

        public string Id { get; private set; }

        public string Version { get; private set; }

        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ Version.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is FunctionDataCacheKey other))
            {
                return false;
            }

            return Id == other.Id && Version == other.Version;
        }
    }
}
