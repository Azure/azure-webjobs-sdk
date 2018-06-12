// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class JobHostOptionsTests
    {
        [Fact]
        public void ConstructorDefaults()
        {
            JobHostOptions config = new JobHostOptions();
            Assert.Null(config.HostId);
        }

        [Fact]
        public void HostId_IfNull_DoesNotThrow()
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();
            string hostId = null;

            // Act & Assert
            configuration.HostId = hostId;
        }

        [Fact]
        public void HostId_IfValid_DoesNotThrow()
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();
            string hostId = "abc";

            // Act & Assert
            configuration.HostId = hostId;
        }

        [Fact]
        public void HostId_IfMinimumLength_DoesNotThrow()
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();
            string hostId = "a";

            // Act & Assert
            configuration.HostId = hostId;
        }

        [Fact]
        public void HostId_IfMaximumLength_DoesNotThrow()
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();
            const int maximumValidCharacters = 32;
            string hostId = new string('a', maximumValidCharacters);

            // Act & Assert
            configuration.HostId = hostId;
        }

        [Fact]
        public void HostId_IfContainsEveryValidLetter_DoesNotThrow()
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();
            string hostId = "abcdefghijklmnopqrstuvwxyz";

            // Act & Assert
            configuration.HostId = hostId;
        }

        [Fact]
        public void HostId_IfContainsEveryValidOtherCharacter_DoesNotThrow()
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();
            string hostId = "0-123456789";

            // Act & Assert
            configuration.HostId = hostId;
        }

        [Fact]
        public void HostId_IfEmpty_Throws()
        {
            TestHostIdThrows(String.Empty);
        }

        [Fact]
        public void HostId_IfTooLong_Throws()
        {
            const int maximumValidCharacters = 32;
            string hostId = new string('a', maximumValidCharacters + 1);
            TestHostIdThrows(hostId);
        }

        [Fact]
        public void HostId_IfContainsInvalidCharacter_Throws()
        {
            // Uppercase character are not allowed.
            TestHostIdThrows("aBc");
        }

        [Fact]
        public void HostId_IfStartsWithDash_Throws()
        {
            TestHostIdThrows("-abc");
        }

        [Fact]
        public void HostId_IfEndsWithDash_Throws()
        {
            TestHostIdThrows("abc-");
        }

        [Fact]
        public void HostId_IfContainsConsecutiveDashes_Throws()
        {
            TestHostIdThrows("a--bc");
        }


        // TODO: DI: Change to use IHostingEnvironment
        //[Theory]
        //[InlineData(null, false)]
        //[InlineData("Blah", false)]
        //[InlineData("Development", true)]
        //[InlineData("development", true)]
        //public void IsDevelopment_ReturnsCorrectValue(string settingValue, bool expected)
        //{
        //    using (EnvVarHolder.Set(Constants.EnvironmentSettingName, settingValue))
        //    {
        //        JobHostOptions config = new JobHostOptions();
        //        Assert.Equal(config.IsDevelopment, expected);
        //    }
        //}

        //public void UseDevelopmentSettings_ConfiguresCorrectValues()
        //{
        //    using (EnvVarHolder.Set(Constants.EnvironmentSettingName, "Development"))
        //    {
        //        JobHostOptions config = new JobHostOptions();
        //        Assert.False(config.UsingDevelopmentSettings);

        //        if (config.IsDevelopment)
        //        {
        //            config.UseDevelopmentSettings();
        //        }

        //        Assert.True(config.UsingDevelopmentSettings);
        //        Assert.Equal(TimeSpan.FromSeconds(2), config.Queues.MaxPollingInterval);
        //        Assert.Equal(TimeSpan.FromSeconds(15), config.Singleton.ListenerLockPeriod);
        //    }
        //}

        private static void TestHostIdThrows(string hostId)
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();

            // Act & Assert
            ExceptionAssert.ThrowsArgument(() => { configuration.HostId = hostId; }, "value",
                "A host ID must be between 1 and 32 characters, contain only lowercase letters, numbers, and " +
                "dashes, not start or end with a dash, and not contain consecutive dashes.");
        }

        private class FastLogger : IAsyncCollector<FunctionInstanceLogEntry>
        {
            public List<FunctionInstanceLogEntry> List = new List<FunctionInstanceLogEntry>();

            public static FunctionInstanceLogEntry FlushEntry = new FunctionInstanceLogEntry(); // marker for flushes

            public Task AddAsync(FunctionInstanceLogEntry item, CancellationToken cancellationToken = default(CancellationToken))
            {
                if (item.Arguments == null)
                {
                    return Task.CompletedTask;
                }
                var clone = JsonConvert.DeserializeObject<FunctionInstanceLogEntry>(JsonConvert.SerializeObject(item));
                List.Add(clone);
                return Task.CompletedTask;
            }

            public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                List.Add(FlushEntry);
                return Task.CompletedTask;
            }
        }

        // Verify that JobHostConfig pulls a Sas container from appsettings. 
        [Fact]
        public void JobHost_UsesSas()
        {
            var fakeSasUri = "https://contoso.blob.core.windows.net/myContainer?signature=foo";

            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost()
                .ConfigureAppConfiguration(c =>
                {
                    c.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "AzureWebJobsInternalSasBlobContainer", fakeSasUri }
                    });
                })
                .Build();

            var config = host.GetOptions<JobHostInternalStorageOptions>();
            var container = config.InternalContainer;
            Assert.NotNull(container);
            Assert.Equal(container.Name, "myContainer"); // specified in sas. 
        }

        // Test that we can explicitly disable storage and call through a function
        // And enable the fast table logger and ensure that's getting events.
        [Fact]
        public async Task JobHost_NoStorage_Succeeds()
        {
            var fastLogger = new FastLogger();
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<BasicTest>()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IAsyncCollector<FunctionInstanceLogEntry>>(fastLogger);

                    // TODO: We shouldn't have to do this, but our default parser
                    //       does not allow for null Storage/Dashboard. $$$
                    //var mockParser = new Mock<IStorageAccountParser>();
                    //mockParser
                    //    .Setup(p => p.ParseAccount(null, It.IsAny<string>()))
                    //    .Returns<string>(null);

                    //services.AddSingleton<IStorageAccountParser>(mockParser.Object);
                })
                .ConfigureAppConfiguration(config =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string>()
                    {
                        { "AzureWebJobsStorage", null },
                        { "AzureWebJobsDashboard", null }
                    });
                })
                .AddStorageBindings()
                .Build();

            Assert.Null(host.GetOptions<JobHostInternalStorageOptions>().InternalContainer);

            var randomValue = Guid.NewGuid().ToString();

            StringBuilder sbLoggingCallbacks = new StringBuilder();

            // Manually invoked.
            var method = typeof(BasicTest).GetMethod("Method", BindingFlags.Public | BindingFlags.Static);

            host.GetJobHost().Call(method, new { value = randomValue });
            Assert.True(BasicTest.Called);

            Assert.Equal(2, fastLogger.List.Count); // We should be batching, so flush not called yet.

            // $$$ AddStraoge sets SuperHack, which requires Dashboard connection 
            host.Start(); // required to call stop()
            await host.StopAsync(); // will ensure flush is called.

            // Verify fast logs
            Assert.Equal(3, fastLogger.List.Count);

            var startMsg = fastLogger.List[0];
            Assert.Equal("BasicTest.Method", startMsg.FunctionName);
            Assert.Equal(null, startMsg.EndTime);
            Assert.NotNull(startMsg.StartTime);

            var endMsg = fastLogger.List[1];
            Assert.Equal(startMsg.FunctionName, endMsg.FunctionName);
            Assert.Equal(startMsg.StartTime, endMsg.StartTime);
            Assert.Equal(startMsg.FunctionInstanceId, endMsg.FunctionInstanceId);
            Assert.NotNull(endMsg.EndTime); // signal completed
            Assert.True(endMsg.StartTime <= endMsg.EndTime);
            Assert.Null(endMsg.ErrorDetails);
            Assert.Null(endMsg.ParentId);

            Assert.Equal(2, endMsg.Arguments.Count);
            Assert.True(endMsg.Arguments.ContainsKey("log"));
            Assert.Equal(randomValue, endMsg.Arguments["value"]);
            Assert.Equal("val=" + randomValue, endMsg.LogOutput.Trim());

            Assert.Same(FastLogger.FlushEntry, fastLogger.List[2]);
        }

        public class BasicTest
        {
            public static bool Called = false;

            [NoAutomaticTrigger]
            public static void Method(TextWriter log, string value)
            {
                log.Write("val={0}", value);
                Called = true;
            }
        }
    }
}