// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.TestCommon;
using System.Threading.Tasks;
using System;
using Xunit;
using Microsoft.Azure.WebJobs.Host.Scale;
using Moq;
using Microsoft.Extensions.Primitives;
using System.Threading;
using System.Collections.Generic;
using Azure.Core;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class DynamicTargetValueProviderTest
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvier;

        public DynamicTargetValueProviderTest()
        {
            _loggerFactory = new LoggerFactory();
            _loggerProvier = new TestLoggerProvider();
            _loggerFactory.AddProvider(_loggerProvier);
        }

        [Fact]
        public async Task GetDynamicTargetValue_NoRepository()
        {
            DynamicTargetValueProvider dynamicTargetValueProvider = new DynamicTargetValueProvider(null, _loggerFactory);
            int result = await dynamicTargetValueProvider.GetDynamicTargetValueAsync("func1");
            Assert.Contains("Returning fallback. Snapshot repository does not exists.", _loggerProvier.GetAllLogMessages().Select(x => x.FormattedMessage));
            Assert.Equal(result, -1);
        }

        [Theory]
        [InlineData(60, 60, "func1", 1)] // getting value from cache
        [InlineData(60, 45, "func1", 1)] // getting value from cache
        [InlineData(60, 35, "func1", -1)] // snapshot is expired
        [InlineData(20, 60, "func1", 4)] // getting value from last snapshot
        [InlineData(60, 60, "func4", -1)] // funciton is not found in the spapshot
        public async Task GetDynamicTargetValue_ReturnsExpected(
            int cachedIntervalInSec,
            int expiredSnapshotIntervalInSec,
            string functionName,
            int expectedConcurrency)
        {
            // Set last snapshot
            IConcurrencyStatusRepository repo = CreateConcurrencyStatusRepository(DateTime.Now.AddSeconds(-40),
                new string[] { "func1", "func2", "func3" },
                new int[] { 1, 2, 3 });
            DynamicTargetValueProvider dynamicTargetValueProvider = new DynamicTargetValueProvider(repo, _loggerFactory, 
                TimeSpan.FromSeconds(cachedIntervalInSec), TimeSpan.FromSeconds(expiredSnapshotIntervalInSec));
            await dynamicTargetValueProvider.GetDynamicTargetValueAsync(functionName);
            dynamicTargetValueProvider.LastSnapshotRead = DateTime.Now.AddSeconds(-30);

            DateTime now = DateTime.Now;

            repo = CreateConcurrencyStatusRepository(DateTime.Now.AddSeconds(-20),
                new string[] { "func1", "func2", "func3" },
                new int[] { 4, 5, 6 });
            dynamicTargetValueProvider.ConcurrencyStatusRepository = repo;
            int result = await dynamicTargetValueProvider.GetDynamicTargetValueAsync(functionName);

            var logs = _loggerProvier.GetAllLogMessages().ToArray();
            Assert.Equal(result, expectedConcurrency);
        }

        private IConcurrencyStatusRepository CreateConcurrencyStatusRepository(DateTime timestamp, string[] functionNames, int[] concurrecnies)
        {
            HostConcurrencySnapshot hostConcurrencySnapshot = new HostConcurrencySnapshot()
            {
                Timestamp = timestamp
            };

            var functionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>();
            for (int i = 0; i < functionNames.Length; i++)
            {
                functionSnapshots[functionNames[i]] = new FunctionConcurrencySnapshot() { Concurrency = concurrecnies[i] };
            }
            hostConcurrencySnapshot.FunctionSnapshots = functionSnapshots;

            Mock<IConcurrencyStatusRepository> mockoncurrencyStatusRepository = new Mock<IConcurrencyStatusRepository>();
            mockoncurrencyStatusRepository.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(hostConcurrencySnapshot);
            return mockoncurrencyStatusRepository.Object;
        }
    }
}
