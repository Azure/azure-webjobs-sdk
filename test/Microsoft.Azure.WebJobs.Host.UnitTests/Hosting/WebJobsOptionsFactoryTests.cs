// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Hosting
{
    public class WebJobsOptionsFactoryTests
    {
        [Fact]
        public async Task Factory_LogsOptions()
        {
            IOptionsLoggingSource source = new OptionsLoggingSource();

            IOptionsFactory<TestOptions> factory = new WebJobsOptionsFactory<TestOptions>(GetOptionsFactory<TestOptions>(), source);

            TestOptions options = factory.Create(null);

            source.LogStream.Complete();

            IList<string> logs = new List<string>();
            ISourceBlock<string> logStream = source.LogStream;
            while (await logStream.OutputAvailableAsync(CancellationToken.None))
            {
                logs.Add(await logStream.ReceiveAsync());
            }

            string log = logs.Single();

            string expected =
               "TestOptions" + Environment.NewLine +
               "{" + Environment.NewLine +
               "  \"SomeValue\": \"abc123\"" + Environment.NewLine +
               "}";

            Assert.Equal(expected, log);
        }

        [Fact]
        public async Task Factory_LogsFrameworkOptions()
        {
            IOptionsLoggingSource source = new OptionsLoggingSource();

            IOptionsFactory<LoggerFilterOptions> factory = new WebJobsOptionsFactory<LoggerFilterOptions>(
                GetOptionsFactory<LoggerFilterOptions>(),
                source,
                new LoggerFilterOptionsFormatter());

            LoggerFilterOptions options = factory.Create(null);

            source.LogStream.Complete();

            IList<string> logs = new List<string>();
            ISourceBlock<string> logStream = source.LogStream;
            while (await logStream.OutputAvailableAsync(CancellationToken.None))
            {
                logs.Add(await logStream.ReceiveAsync());
            }

            string log = logs.Single();

            string expected =
               "LoggerFilterOptions" + Environment.NewLine +
               "{" + Environment.NewLine +
               "  \"MinLevel\": \"Trace\"," + Environment.NewLine +
               "  \"Rules\": []" + Environment.NewLine +
               "}";

            Assert.Equal(expected, log);
        }

        private static OptionsFactory<T> GetOptionsFactory<T>() where T : class, new() =>
            new OptionsFactory<T>(Enumerable.Empty<IConfigureOptions<T>>(), Enumerable.Empty<IPostConfigureOptions<T>>());

        private class TestOptions : IOptionsFormatter
        {
            public string SomeValue { get; set; } = "abc123";

            public string SomeSecret { get; set; } = "def456";

            public string Format()
            {
                JObject options = new JObject
                {
                    { nameof(SomeValue), SomeValue }
                };

                return options.ToString(Formatting.Indented);
            }
        }
    }
}
