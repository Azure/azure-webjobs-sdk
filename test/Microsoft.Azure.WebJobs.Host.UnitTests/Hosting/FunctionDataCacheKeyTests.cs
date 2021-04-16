// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Hosting
{
    public class FunctionDataCacheKeyTests
    {
        [Fact]
        public void GetHashCode_UsesIdAndVersion()
        {
            string id = "foo";
            string version = "bar";
            FunctionDataCacheKey key = new FunctionDataCacheKey(id, version);

            int hashId = id.GetHashCode();
            int versionHash = version.GetHashCode();
            int combinedHash = hashId ^ versionHash;

            Assert.Equal(combinedHash, key.GetHashCode());
        }

        [Fact]
        public void LookupKeyInSet_EnsureFound()
        {
            HashSet<FunctionDataCacheKey> set = new HashSet<FunctionDataCacheKey>();

            string id = "foo";
            string version = "bar";
            FunctionDataCacheKey key = new FunctionDataCacheKey(id, version);

            Assert.True(set.Add(key));

            Assert.Contains(new FunctionDataCacheKey(id, version), set);
        }

        [Fact]
        public void LookupKeyInSetWithEqualIdUnequalVersion_EnsureNotFound()
        {
            HashSet<FunctionDataCacheKey> set = new HashSet<FunctionDataCacheKey>();

            string id = "foo";
            string version1 = "bar1";
            FunctionDataCacheKey key = new FunctionDataCacheKey(id, version1);

            Assert.True(set.Add(key));

            string version2 = "bar2";
            Assert.DoesNotContain(new FunctionDataCacheKey(id, version2), set);
        }

        [Fact]
        public void LookupKeyInSetWithEqualVersionUnequalId_EnsureNotFound()
        {
            HashSet<FunctionDataCacheKey> set = new HashSet<FunctionDataCacheKey>();

            string id1 = "foo1";
            string version = "bar";
            FunctionDataCacheKey key = new FunctionDataCacheKey(id1, version);

            Assert.True(set.Add(key));

            string id2 = "foo2";
            Assert.DoesNotContain(new FunctionDataCacheKey(id2, version), set);
        }

        [Fact]
        public void CompareEqualKeys_EnsureEqual()
        {
            string id = "foo";
            string version = "bar";
            FunctionDataCacheKey key1 = new FunctionDataCacheKey(id, version);
            FunctionDataCacheKey key2 = new FunctionDataCacheKey(id, version);

            Assert.Equal(key1, key2);
        }

        [Fact]
        public void CompareKeysWithEqualIdUnequalVersion_EnsureUnequal()
        {
            string id = "foo";
            string version1 = "bar1";
            FunctionDataCacheKey key1 = new FunctionDataCacheKey(id, version1);

            string version2 = "bar2";
            FunctionDataCacheKey key2 = new FunctionDataCacheKey(id, version2);

            Assert.NotEqual(key1, key2);
        }

        [Fact]
        public void CompareKeysWithEqualVersionUnequalId_EnsureUnequal()
        {
            string id1 = "foo1";
            string version = "bar";
            FunctionDataCacheKey key1 = new FunctionDataCacheKey(id1, version);

            string id2 = "id2";
            FunctionDataCacheKey key2 = new FunctionDataCacheKey(id2, version);

            Assert.NotEqual(key1, key2);
        }
    }
}
