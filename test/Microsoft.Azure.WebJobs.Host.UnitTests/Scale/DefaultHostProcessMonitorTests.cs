// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using static Microsoft.Azure.WebJobs.Host.UnitTests.Scale.ProcessMonitorTests;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class DefaultHostProcessMonitorTests
    {
        private readonly DefaultHostProcessMonitor _hostProcessMonitor;
        private readonly LoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly ILogger _logger;

        private List<TimeSpan> _testHostProcessorTimeSamples;
        private List<long> _testHostMemorySamples;
        private List<TimeSpan> _testChildProcessorTimeSamples;
        private List<long> _testChildMemorySamples;

        private ProcessStats _testHostProcessStats;
        private ProcessStats _testChildProcessStats;

        public DefaultHostProcessMonitorTests()
        {
            _loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            _loggerFactory.AddProvider(_loggerProvider);
            _logger = _loggerFactory.CreateLogger(LogCategories.Concurrency);

            var options = new ConcurrencyOptions
            {
                DynamicConcurrencyEnabled = true,
                TotalAvailableMemoryBytes = 10000
            };
            var optionsWrapper = new OptionsWrapper<ConcurrencyOptions>(options);
            var hostTestProcess = new TestProcess(1, _testHostProcessorTimeSamples, _testHostMemorySamples);
            var hostProcessMonitor = new Mock<ProcessMonitor>(MockBehavior.Strict);
            hostProcessMonitor.Setup(p => p.Process).Returns(hostTestProcess);
            hostProcessMonitor.Setup(p => p.GetStats()).Returns(() => _testHostProcessStats);
            _hostProcessMonitor = new DefaultHostProcessMonitor(optionsWrapper, hostProcessMonitor.Object);

            // add a child process monitor
            var childTestProcess = new TestProcess(2, _testChildProcessorTimeSamples, _testChildMemorySamples);
            var childProcessMonitor = new Mock<ProcessMonitor>(MockBehavior.Strict);
            childProcessMonitor.Setup(p => p.Process).Returns(childTestProcess);
            childProcessMonitor.Setup(p => p.GetStats()).Returns(() => _testChildProcessStats);
            _hostProcessMonitor.RegisterChildProcessMonitor(childProcessMonitor.Object);

            _testHostProcessorTimeSamples = new List<TimeSpan>();
            _testHostMemorySamples = new List<long>();

            _testChildProcessorTimeSamples = new List<TimeSpan>();
            _testChildMemorySamples = new List<long>();

            _testHostProcessStats = new ProcessStats
            {
                ProcessId = 1,
                CpuLoadHistory = new double[0],
                MemoryUsageHistory = new long[0]
            };

            _testChildProcessStats = new ProcessStats
            {
                ProcessId = 2,
                CpuLoadHistory = new double[0],
                MemoryUsageHistory = new long[0]
            };
        }

        [Fact]
        public void GetStatus_SingleProcess_LowCpu_ReturnsOk()
        {
            _testHostProcessStats = new ProcessStats
            {
                ProcessId = 1,
                CpuLoadHistory = new List<double> { 50, 45, 40, 30, 35, 40, 40, 30, 35, 25 },
                MemoryUsageHistory = new long[0]
            };

            var status = _hostProcessMonitor.GetStatus(_logger);

            Assert.Equal(HostHealthState.Ok, status.State);
            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(2, logs.Length);

            var log = logs[0];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host process CPU stats (PID 1): History=(40,40,30,35,25), AvgCpuLoad=34, MaxCpuLoad=40", log.FormattedMessage);

            log = logs[1];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host aggregate CPU load 34", log.FormattedMessage);
        }

        [Fact]
        public void GetStatus_SingleProcess_LowMemory_ReturnsOk()
        {
            _testHostProcessStats = new ProcessStats
            {
                ProcessId = 1,
                CpuLoadHistory = new double[0],
                MemoryUsageHistory = new List<long> { 2000, 2100, 2500, 2500, 3000, 2700, 2200, 1500, 2000, 2500 }
            };

            var status = _hostProcessMonitor.GetStatus(_logger);

            Assert.Equal(HostHealthState.Ok, status.State);
            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(2, logs.Length);

            var log = logs[0];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host process memory usage (PID 1): History=(2700,2200,1500,2000,2500), AvgUsage=2180, MaxUsage=2700", log.FormattedMessage);

            log = logs[1];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host aggregate memory usage 2500 (31% of threshold)", log.FormattedMessage);
        }

        [Fact]
        public void GetStatus_SingleProcess_HighCpu_ReturnsOverloaded()
        {
            _testHostProcessStats = new ProcessStats
            {
                ProcessId = 1,
                CpuLoadHistory = new List<double> { 50, 100, 85, 95, 80, 70, 80, 90, 95, 100 },
                MemoryUsageHistory = new long[0]
            };

            var status = _hostProcessMonitor.GetStatus(_logger);

            Assert.Equal(HostHealthState.Overloaded, status.State);
            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(3, logs.Length);

            var log = logs[0];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host process CPU stats (PID 1): History=(70,80,90,95,100), AvgCpuLoad=87, MaxCpuLoad=100", log.FormattedMessage);

            log = logs[1];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host aggregate CPU load 87", log.FormattedMessage);

            log = logs[2];
            Assert.Equal(LogLevel.Warning, log.Level);
            Assert.Equal("[HostMonitor] Host CPU threshold exceeded (87 >= 80)", log.FormattedMessage);
        }

        [Fact]
        public void GetStatus_SingleProcess_HighMemory_ReturnsOverloaded()
        {
            _testHostProcessStats = new ProcessStats
            {
                ProcessId = 1,
                CpuLoadHistory = new double[0],
                MemoryUsageHistory = new List<long> { 2000, 2100, 2500, 4000, 7000, 8000, 8500, 8600, 8750, 8800 }
            };

            var status = _hostProcessMonitor.GetStatus(_logger);

            Assert.Equal(HostHealthState.Overloaded, status.State);
            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(3, logs.Length);

            var log = logs[0];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host process memory usage (PID 1): History=(8000,8500,8600,8750,8800), AvgUsage=8530, MaxUsage=8800", log.FormattedMessage);

            log = logs[1];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host aggregate memory usage 8800 (110% of threshold)", log.FormattedMessage);

            log = logs[2];
            Assert.Equal(LogLevel.Warning, log.Level);
            Assert.Equal("[HostMonitor] Host memory threshold exceeded (8800 >= 8000)", log.FormattedMessage);
        }

        [Fact]
        public void GetStatus_MultiProcess_LowCpu_ReturnsOk()
        {
            _testHostProcessStats = new ProcessStats
            {
                ProcessId = 1,
                CpuLoadHistory = new List<double> { 0, 5, 10, 30, 35, 40, 30, 20, 10, 0 },
                MemoryUsageHistory = new long[0]
            };

            _testChildProcessStats = new ProcessStats
            {
                ProcessId = 2,
                CpuLoadHistory = new List<double> { 10, 15, 10, 25, 15, 20, 10, 15, 10, 5 },
                MemoryUsageHistory = new long[0]
            };

            var status = _hostProcessMonitor.GetStatus(_logger);

            Assert.Equal(HostHealthState.Ok, status.State);
            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(3, logs.Length);

            var log = logs[0];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host process CPU stats (PID 2): History=(20,10,15,10,5), AvgCpuLoad=12, MaxCpuLoad=20", log.FormattedMessage);

            log = logs[1];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host process CPU stats (PID 1): History=(40,30,20,10,0), AvgCpuLoad=20, MaxCpuLoad=40", log.FormattedMessage);

            log = logs[2];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host aggregate CPU load 32", log.FormattedMessage);
        }

        [Fact]
        public void GetStatus_MultiProcess_LowMemory_ReturnsOk()
        {
            _testHostProcessStats = new ProcessStats
            {
                ProcessId = 1,
                CpuLoadHistory = new double[0],
                MemoryUsageHistory = new List<long> { 2000, 2100, 2500, 2500, 3000, 2700, 2200, 1500, 2000, 2500 }
            };

            _testChildProcessStats = new ProcessStats
            {
                ProcessId = 2,
                CpuLoadHistory = new double[0],
                MemoryUsageHistory = new List<long> { 800, 900, 1000, 950, 900, 800, 800, 850, 900, 1000 }
            };

            var status = _hostProcessMonitor.GetStatus(_logger);

            Assert.Equal(HostHealthState.Ok, status.State);
            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(3, logs.Length);

            var log = logs[0];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host process memory usage (PID 2): History=(800,800,850,900,1000), AvgUsage=870, MaxUsage=1000", log.FormattedMessage);

            log = logs[1];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host process memory usage (PID 1): History=(2700,2200,1500,2000,2500), AvgUsage=2180, MaxUsage=2700", log.FormattedMessage);

            log = logs[2];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host aggregate memory usage 3500 (43% of threshold)", log.FormattedMessage);
        }

        [Fact]
        public void GetStatus_MultiProcess_HighCpu_ReturnsOverloaded()
        {
            _testHostProcessStats = new ProcessStats
            {
                ProcessId = 1,
                CpuLoadHistory = new List<double> { 20, 10, 30, 50, 50, 60, 50, 40, 45, 50 },
                MemoryUsageHistory = new long[0]
            };

            _testChildProcessStats = new ProcessStats
            {
                ProcessId = 2,
                CpuLoadHistory = new List<double> { 10, 15, 10, 25, 30, 30, 35, 40, 45, 45 },
                MemoryUsageHistory = new long[0]
            };

            var status = _hostProcessMonitor.GetStatus(_logger);

            Assert.Equal(HostHealthState.Overloaded, status.State);
            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(4, logs.Length);

            var log = logs[0];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host process CPU stats (PID 2): History=(30,35,40,45,45), AvgCpuLoad=39, MaxCpuLoad=45", log.FormattedMessage);

            log = logs[1];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host process CPU stats (PID 1): History=(60,50,40,45,50), AvgCpuLoad=49, MaxCpuLoad=60", log.FormattedMessage);

            log = logs[2];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host aggregate CPU load 88", log.FormattedMessage);

            log = logs[3];
            Assert.Equal(LogLevel.Warning, log.Level);
            Assert.Equal("[HostMonitor] Host CPU threshold exceeded (88 >= 80)", log.FormattedMessage);
        }

        [Fact]
        public void GetStatus_MultiProcess_HighMemory_ReturnsOverloaded()
        {
            _testHostProcessStats = new ProcessStats
            {
                ProcessId = 1,
                CpuLoadHistory = new double[0],
                MemoryUsageHistory = new List<long> { 2000, 2100, 2500, 4000, 4000, 4200, 4300, 4300, 4000, 4500 }
            };

            _testChildProcessStats = new ProcessStats
            {
                ProcessId = 2,
                CpuLoadHistory = new double[0],
                MemoryUsageHistory = new List<long> { 800, 900, 1000, 950, 1000, 1500, 2000, 3000, 3100, 4000 }
            };

            var status = _hostProcessMonitor.GetStatus(_logger);

            Assert.Equal(HostHealthState.Overloaded, status.State);
            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(4, logs.Length);

            var log = logs[0];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host process memory usage (PID 2): History=(1500,2000,3000,3100,4000), AvgUsage=2720, MaxUsage=4000", log.FormattedMessage);

            log = logs[1];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host process memory usage (PID 1): History=(4200,4300,4300,4000,4500), AvgUsage=4260, MaxUsage=4500", log.FormattedMessage);

            log = logs[2];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal("[HostMonitor] Host aggregate memory usage 8500 (106% of threshold)", log.FormattedMessage);

            log = logs[3];
            Assert.Equal(LogLevel.Warning, log.Level);
            Assert.Equal("[HostMonitor] Host memory threshold exceeded (8500 >= 8000)", log.FormattedMessage);
        }

        [Fact]
        public async Task ChildProcessManagement_ExitedProcessesAreRemoved()
        {
            var options = new ConcurrencyOptions();
            var localProcessMonitor = new DefaultHostProcessMonitor(new OptionsWrapper<ConcurrencyOptions>(options));
            var hostProcess = Process.GetCurrentProcess();

            int numChildProcesses = 3;
            List<TestProcess> childProcesses = new List<TestProcess>();
            for (int i = 1; i <= numChildProcesses; i++)
            {
                var childProcess = new TestProcess(i);
                childProcesses.Add(childProcess);
            }

            Assert.True(childProcesses.All(p => !p.HasExited));

            foreach (var currProcess in childProcesses)
            {
                var childMonitor = new ProcessMonitor(currProcess);
                localProcessMonitor.RegisterChildProcessMonitor(childMonitor);
            }

            // initial call to get status which will start the monitoring
            localProcessMonitor.GetStatus(_logger);

            // wait for enough samples to accululate
            await Task.Delay(TimeSpan.FromSeconds(ProcessMonitor.DefaultSampleIntervalSeconds * 2 * DefaultHostProcessMonitor.MinSampleCount));

            // verify all child processes are being monitored
            localProcessMonitor.GetStatus(_logger);
            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(2 + numChildProcesses, logs.Length);
            for (int i = 0; i < numChildProcesses; i++)
            {
                Assert.True(logs[i].FormattedMessage.StartsWith($"[HostMonitor] Host process CPU stats (PID {childProcesses[i].Id})"));
            }
            Assert.True(logs[numChildProcesses].FormattedMessage.StartsWith($"[HostMonitor] Host process CPU stats (PID {hostProcess.Id})"));
            Assert.True(logs[numChildProcesses + 1].FormattedMessage.StartsWith("[HostMonitor] Host aggregate CPU load"));

            // now kill one of the child processes
            var killedProcess = childProcesses[1];
            killedProcess.Kill();

            // wait for the process to exit fully and give time for a couple more samples to be taken
            await TestHelpers.Await(() =>
            {
                return killedProcess.HasExited;
            });
            await Task.Delay(TimeSpan.FromSeconds(ProcessMonitor.DefaultSampleIntervalSeconds + 1));

            // verify the killed process is no longer being monitored
            _loggerProvider.ClearAllLogMessages();
            localProcessMonitor.GetStatus(_logger);
            logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(2 + numChildProcesses - 1, logs.Length);
            Assert.Empty(logs.Where(p => p.FormattedMessage.Contains($"[HostMonitor] Host process CPU stats (PID {killedProcess.Id})")));
        }

        [Fact]
        public void ChildProcessManagement_Register_Unregister()
        {
            var childProcess1 = new Process();
            var childProcess2 = new Process();
            var childProcess3 = new Process();

            var options = new ConcurrencyOptions();
            var localProcessMonitor = new DefaultHostProcessMonitor(new OptionsWrapper<ConcurrencyOptions>(options));
            Assert.Empty(localProcessMonitor.ChildProcessMonitors);

            localProcessMonitor.RegisterChildProcess(childProcess1);
            localProcessMonitor.RegisterChildProcess(childProcess2);
            localProcessMonitor.RegisterChildProcess(childProcess3);
            Assert.Equal(3, localProcessMonitor.ChildProcessMonitors.Count);

            localProcessMonitor.UnregisterChildProcess(childProcess2);
            Assert.Equal(2, localProcessMonitor.ChildProcessMonitors.Count);
        }
    }
}
