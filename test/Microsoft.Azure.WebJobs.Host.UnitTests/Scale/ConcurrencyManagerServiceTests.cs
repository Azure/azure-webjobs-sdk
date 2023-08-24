// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Linq;
using Xunit;
using System.Timers;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class ConcurrencyManagerServiceTests
    {
        private readonly LoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly Mock<ConcurrencyManager> _concurrencyManagerMock;
        private readonly Mock<IConcurrencyStatusRepository> _repositoryMock;
        private readonly Mock<IPrimaryHostStateProvider> _primaryHostStateProviderMock;
        private readonly ConcurrencyManagerService _concurrencyManagerService;
        private readonly ConcurrencyOptions _concurrencyOptions;
        private readonly HostConcurrencySnapshot _snapshot;

        private bool _isPrimaryHost;

        public ConcurrencyManagerServiceTests()
        {
            _isPrimaryHost = false;
            _concurrencyOptions = new ConcurrencyOptions
            {
                DynamicConcurrencyEnabled = true
            };
            var optionsWrapper = new OptionsWrapper<ConcurrencyOptions>(_concurrencyOptions);
            _loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            _loggerFactory.AddProvider(_loggerProvider);

            _concurrencyManagerMock = new Mock<ConcurrencyManager>(MockBehavior.Strict);
            _repositoryMock = new Mock<IConcurrencyStatusRepository>(MockBehavior.Strict);

            _snapshot = new HostConcurrencySnapshot
            {
                NumberOfCores = 4,
                Timestamp = DateTime.UtcNow
            };
            _snapshot.FunctionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>
            {
                { "function0", new FunctionConcurrencySnapshot { Concurrency = 5 } },
                { "function1", new FunctionConcurrencySnapshot { Concurrency = 10 } },
                { "function2", new FunctionConcurrencySnapshot { Concurrency = 15 } },
                { "testsharedid", new FunctionConcurrencySnapshot { Concurrency = 20 } }
            };

            // add a few normal functions
            List<IFunctionDefinition> functions = new List<IFunctionDefinition>();
            for (int i = 0; i < 3; i++)
            {
                var functionDefinitionMock = new Mock<IFunctionDefinition>(MockBehavior.Strict);
                functionDefinitionMock.Setup(p => p.Descriptor).Returns(new FunctionDescriptor { Id = $"function{i}" });
                functions.Add(functionDefinitionMock.Object); 
            }

            // add a few functions using the shared listener pattern
            for (int i = 3; i < 6; i++)
            {
                var functionDefinitionMock = new Mock<IFunctionDefinition>(MockBehavior.Strict);
                functionDefinitionMock.Setup(p => p.Descriptor).Returns(new FunctionDescriptor { Id = $"function{i}", SharedListenerId = "testsharedid" });
                functions.Add(functionDefinitionMock.Object);
            }

            var functionIndexMock = new Mock<IFunctionIndex>(MockBehavior.Strict);
            functionIndexMock.Setup(p => p.ReadAll()).Returns(functions);

            var functionIndexProviderMock = new Mock<IFunctionIndexProvider>(MockBehavior.Strict);
            functionIndexProviderMock.Setup(p => p.GetAsync(CancellationToken.None)).ReturnsAsync(functionIndexMock.Object);

            _primaryHostStateProviderMock = new Mock<IPrimaryHostStateProvider>(MockBehavior.Strict);
            _primaryHostStateProviderMock.SetupGet(p => p.IsPrimary).Returns(() => _isPrimaryHost);

            _concurrencyManagerService = new ConcurrencyManagerService(optionsWrapper, _loggerFactory, _concurrencyManagerMock.Object, _repositoryMock.Object, functionIndexProviderMock.Object, _primaryHostStateProviderMock.Object);
        }

        [Fact]
        public async Task StartAsync_DynamicConcurrencyDisabled_DoesNotStart()
        {
            _concurrencyOptions.DynamicConcurrencyEnabled = false;

            await _concurrencyManagerService.StartAsync(CancellationToken.None);

            Assert.False(_concurrencyManagerService.StatusPersistenceTimer.Enabled);
        }

        [Fact]
        public async Task StartAsync_SnapshotPersistenceDisabled_DoesNotStart()
        {
            _concurrencyOptions.DynamicConcurrencyEnabled = true;
            _concurrencyOptions.SnapshotPersistenceEnabled = false;

            await _concurrencyManagerService.StartAsync(CancellationToken.None);

            Assert.False(_concurrencyManagerService.StatusPersistenceTimer.Enabled);
        }

        [Fact]
        public async Task StartAsync_SnapshotPersistenceEnabled_AppliesSnapshotAndStarts()
        {
            _concurrencyOptions.DynamicConcurrencyEnabled = true;
            _concurrencyOptions.SnapshotPersistenceEnabled = true;

            var snapshot = new HostConcurrencySnapshot();
            _repositoryMock.Setup(p => p.ReadAsync(CancellationToken.None)).ReturnsAsync(snapshot);

            _concurrencyManagerMock.Setup(p => p.ApplySnapshot(snapshot));

            await _concurrencyManagerService.StartAsync(CancellationToken.None);

            _repositoryMock.VerifyAll();
            _concurrencyManagerMock.VerifyAll();

            Assert.True(_concurrencyManagerService.StatusPersistenceTimer.Enabled);
        }

        [Fact]
        public async Task StartAsync_Throws_SnapshotNotApplied_Starts()
        {
            _concurrencyOptions.DynamicConcurrencyEnabled = true;
            _concurrencyOptions.SnapshotPersistenceEnabled = true;

            _repositoryMock.Setup(p => p.ReadAsync(CancellationToken.None)).ThrowsAsync(new Exception("Kaboom!"));

            await _concurrencyManagerService.StartAsync(CancellationToken.None);

            Assert.True(_concurrencyManagerService.StatusPersistenceTimer.Enabled);

            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal("Error applying concurrency snapshot.", log.FormattedMessage);
        }

        [Fact]
        public async Task StopAsync_Stops()
        {
            _concurrencyOptions.DynamicConcurrencyEnabled = true;
            _concurrencyOptions.SnapshotPersistenceEnabled = true;

            var snapshot = new HostConcurrencySnapshot();
            _repositoryMock.Setup(p => p.ReadAsync(CancellationToken.None)).ReturnsAsync(snapshot);

            _concurrencyManagerMock.Setup(p => p.ApplySnapshot(snapshot));

            await _concurrencyManagerService.StartAsync(CancellationToken.None);

            Assert.True(_concurrencyManagerService.StatusPersistenceTimer.Enabled);

            await _concurrencyManagerService.StopAsync(CancellationToken.None);

            Assert.False(_concurrencyManagerService.StatusPersistenceTimer.Enabled);
        }

        [Fact]
        public async Task OnPersistenceTimer_NonPrimary_DoesNotWriteSnapshot()
        {
            _isPrimaryHost = false;

            await _concurrencyManagerService.OnPersistenceTimer();

            Assert.True(_concurrencyManagerService.StatusPersistenceTimer.Enabled);
        }

        [Fact]
        public async Task OnPersistenceTimer_Primary_WritesSnapshot()
        {
            _isPrimaryHost = true;

            _concurrencyManagerMock.Setup(p => p.GetSnapshot()).Returns(_snapshot);

            _repositoryMock.Setup(p => p.WriteAsync(_snapshot, CancellationToken.None)).Returns(Task.CompletedTask);

            await _concurrencyManagerService.OnPersistenceTimer();

            _repositoryMock.VerifyAll();
            _concurrencyManagerMock.VerifyAll();

            Assert.True(_concurrencyManagerService.StatusPersistenceTimer.Enabled);
            Assert.Equal(4, _snapshot.FunctionSnapshots.Count);
        }

        [Fact]
        public async Task WriteSnapshotAsync_RemovesStaleFunctions()
        {
            _isPrimaryHost = true;

            // snapshot includes one function that isn't in the current index
            _snapshot.FunctionSnapshots["function3"] = new FunctionConcurrencySnapshot { Concurrency = 15 };

            _concurrencyManagerMock.Setup(p => p.GetSnapshot()).Returns(_snapshot);

            _repositoryMock.Setup(p => p.WriteAsync(_snapshot, CancellationToken.None)).Returns(Task.CompletedTask);

            await _concurrencyManagerService.OnPersistenceTimer();

            _repositoryMock.VerifyAll();
            _concurrencyManagerMock.VerifyAll();

            Assert.True(_concurrencyManagerService.StatusPersistenceTimer.Enabled);
            Assert.Equal(4, _snapshot.FunctionSnapshots.Count);
            Assert.Collection(_snapshot.FunctionSnapshots,
                p => Assert.Equal("function0", p.Key),
                p => Assert.Equal("function1", p.Key),
                p => Assert.Equal("function2", p.Key),
                p => Assert.Equal("testsharedid", p.Key));
        }

        [Fact]
        public async Task WriteSnapshotAsync_NoChanges_DoesNotWriteSnapshot()
        {
            _isPrimaryHost = true;

            _concurrencyManagerMock.Setup(p => p.GetSnapshot()).Returns(_snapshot);

            _repositoryMock.Setup(p => p.WriteAsync(_snapshot, CancellationToken.None)).Returns(Task.CompletedTask);

            await _concurrencyManagerService.OnPersistenceTimer();
            await _concurrencyManagerService.OnPersistenceTimer();

            _repositoryMock.VerifyAll();
            _repositoryMock.Verify(p => p.WriteAsync(_snapshot, CancellationToken.None), Times.Once);
            _concurrencyManagerMock.VerifyAll();
        }
    }
}
