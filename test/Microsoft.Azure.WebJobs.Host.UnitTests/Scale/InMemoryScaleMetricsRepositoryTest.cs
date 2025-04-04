// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.UnitTests.Scale;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace ScaleController.Tests
{
    public class InMemoryScaleMetricsRepositoryTest
    {
        private readonly InMemoryScaleMetricsRepository _repository;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly ILoggerFactory _loggerFactory;

        public InMemoryScaleMetricsRepositoryTest()
        {
            _loggerProvider = new TestLoggerProvider();
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddProvider(_loggerProvider);

            _repository = new InMemoryScaleMetricsRepository(Options.Create(new ScaleOptions()), _loggerFactory);
        }

        [Fact]
        public async Task WriteMetricsAsync_PersistsMetrics()
        {
            var monitor1 = new TestScaleMonitor1();
            var monitor2 = new TestScaleMonitor2();
            var monitor3 = new TestScaleMonitor3();
            var monitors = new IScaleMonitor[] { monitor1, monitor2, monitor3 };

            var result = await _repository.ReadMetricsAsync(monitors);
            Assert.Equal(3, result.Count);

            // simulate 10 sample iterations
            var start = DateTime.UtcNow;
            for (int i = 0; i < 10; i++)
            {
                Dictionary<IScaleMonitor, ScaleMetrics> metricsMap = new Dictionary<IScaleMonitor, ScaleMetrics>();

                metricsMap.Add(monitor1, new TestScaleMetrics1 { Count = i });
                metricsMap.Add(monitor2, new TestScaleMetrics2 { Num = i });
                metricsMap.Add(monitor3, new TestScaleMetrics3 { Length = i, TimeSpan = DateTime.UtcNow - start });

                await _repository.WriteMetricsAsync(metricsMap);
            }

            // read the metrics back
            result = await _repository.ReadMetricsAsync(monitors);
            Assert.Equal(3, result.Count);

            var monitorMetricsList = result[monitor1];
            for (int i = 0; i < 10; i++)
            {
                var currSample = (TestScaleMetrics1)monitorMetricsList[i];
                Assert.Equal(i, currSample.Count);
                Assert.NotEqual(default(DateTime), currSample.Timestamp);
            }

            monitorMetricsList = result[monitor2];
            for (int i = 0; i < 10; i++)
            {
                var currSample = (TestScaleMetrics2)monitorMetricsList[i];
                Assert.Equal(i, currSample.Num);
                Assert.NotEqual(default(DateTime), currSample.Timestamp);
            }

            monitorMetricsList = result[monitor3];
            for (int i = 0; i < 10; i++)
            {
                var currSample = (TestScaleMetrics3)monitorMetricsList[i];
                Assert.Equal(i, currSample.Length);
                Assert.NotEqual(default(DateTime), currSample.Timestamp);
                Assert.NotEqual(default(TimeSpan), currSample.TimeSpan);
            }

            // if no monitors are presented result will be empty
            monitors = new IScaleMonitor[0];
            result = await _repository.ReadMetricsAsync(monitors);
            Assert.Empty(result);
        }

        [Fact]
        public async Task ReadMetricsAsync_NoMetricsForMonitor_ReturnsEmpty()
        {
            var monitor1 = new TestScaleMonitor1();
            var monitors = new IScaleMonitor[] { monitor1 };

            var result = await _repository.ReadMetricsAsync(monitors);
            Assert.Single(result);
            Assert.Empty(result[monitor1]);
        }

        [Fact]
        public async Task ReadMetricsAsync_InvalidMonitor_ReturnsEmpty()
        {
            var monitor1 = new TestInvalidScaleMonitor();
            var monitors = new IScaleMonitor[] { monitor1 };

            var result = await _repository.ReadMetricsAsync(monitors);
            Assert.Single(result);
            Assert.Empty(result[monitor1]);

            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal($"Monitor {typeof(TestInvalidScaleMonitor).FullName} doesn't implement Microsoft.Azure.WebJobs.Host.Scale.IScaleMonitor`1[TMetrics].", log.FormattedMessage);
        }

        [Theory]
        [InlineData(true, 0, 0)]
        [InlineData(false, 0, 1)]
        [InlineData(true, 10000, 1)]
        public async Task ReadMetricsAsync_PurgeWorks(bool purgeEnabled, int maxAgeInMs, int expectedCount)
        {
            ScaleOptions options = new ScaleOptions()
            {
                MetricsPurgeEnabled = purgeEnabled
            };
            FieldInfo fieldInfo = typeof(ScaleOptions).GetField("_scaleMetricsMaxAge", BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo.SetValue(options, TimeSpan.FromMilliseconds(maxAgeInMs));
            var repository = new InMemoryScaleMetricsRepository(Options.Create(options), _loggerFactory);
            var monitor1 = new TestScaleMonitor1();

            // write metrics
            Dictionary<IScaleMonitor, ScaleMetrics> metricsMap = new Dictionary<IScaleMonitor, ScaleMetrics>()
            {
                { monitor1, new TestScaleMetrics1 { Count = 1 } }
            };
            await repository.WriteMetricsAsync(metricsMap);

            var result = await repository.ReadMetricsAsync(new IScaleMonitor[] { monitor1 });
            Assert.Equal(result.ToArray()[0].Value.Count(), expectedCount);
        }

        [Fact]
        public async Task ReadMetricsAsync_ThreadSafe()
        {
            ScaleOptions options = new ScaleOptions()
            {
                MetricsPurgeEnabled = true
            };
            FieldInfo fieldInfo = typeof(ScaleOptions).GetField("_scaleMetricsMaxAge", BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo.SetValue(options, TimeSpan.FromMilliseconds(1000));
            var repository = new InMemoryScaleMetricsRepository(Options.Create(options), _loggerFactory);
            var monitor1 = new TestScaleMonitor1();
            int count = 100000;

            Task writeTask1 = Task.Run(async () =>
            {
                for (int i = 0; i < count/2; i++)
                {
                    Dictionary<IScaleMonitor, ScaleMetrics> metrics1 = new Dictionary<IScaleMonitor, ScaleMetrics>()
                    {
                        { monitor1, new TestScaleMetrics1 { Count = 1 } }
                    };
                    await repository.WriteMetricsAsync(metrics1);
                }
            });
            Task writeTask2 = Task.Run(async () =>
            {
                Dictionary<IScaleMonitor, ScaleMetrics> metrics1 = new Dictionary<IScaleMonitor, ScaleMetrics>()
                {
                    { monitor1, new TestScaleMetrics1 { Count = 1 } }
                };
                for (int i = 0; i < count/2; i++)
                {
                    await repository.WriteMetricsAsync(metrics1);
                }
            });
            await Task.WhenAll(writeTask1, writeTask2);

            await Task.Delay(TimeSpan.FromMilliseconds(1100));

            for (int i = 0; i < count; i++)
            {
                Dictionary<IScaleMonitor, ScaleMetrics> metrics2 = new Dictionary<IScaleMonitor, ScaleMetrics>()
                {
                    { monitor1, new TestScaleMetrics1 { Count = 2 } }
                };
                await repository.WriteMetricsAsync(metrics2);
            }

           var readTask1 = repository.ReadMetricsAsync(new IScaleMonitor[] { monitor1 });
           var readTask2 = repository.ReadMetricsAsync(new IScaleMonitor[] { monitor1 });

           await Task.WhenAll(readTask1, readTask2);

           Assert.Equal(readTask1.Result.ToArray()[0].Value.Count, count);
           Assert.Equal(((TestScaleMetrics1)readTask1.Result.ToArray()[0].Value[0]).Count, 2);
           Assert.Equal(readTask2.Result.ToArray()[0].Value.Count, count);
           Assert.Equal(((TestScaleMetrics1)readTask2.Result.ToArray()[0].Value[0]).Count, 2);
        }
    }
}
