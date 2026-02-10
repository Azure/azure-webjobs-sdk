// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Hosting
{
    public class AddFormattableOptionsLoggingTests
    {
        [Fact]
        public void NullServices_Throws()
        {
            IServiceCollection services = null;

            Assert.Throws<ArgumentNullException>("services", () => services.AddFormattableOptionsLogging());
        }

        [Fact]
        public void ResolvedOptionsFactory_IsWebJobsOptionsFactory()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddFormattableOptionsLogging();

            using ServiceProvider provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IOptionsFactory<TestOptions>>();

            Assert.IsType<WebJobsOptionsFactory<TestOptions>>(factory);
        }

        [Fact]
        public async Task FormattableOptions_AreLoggedViaSource()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddFormattableOptionsLogging();

            using ServiceProvider provider = services.BuildServiceProvider();

            var source = provider.GetRequiredService<IOptionsLoggingSource>();
            var factory = provider.GetRequiredService<IOptionsFactory<TestOptions>>();

            factory.Create(Options.DefaultName);
            source.LogStream.Complete();

            IList<string> logs = new List<string>();
            while (await source.LogStream.OutputAvailableAsync(CancellationToken.None))
            {
                logs.Add(await source.LogStream.ReceiveAsync());
            }

            string log = Assert.Single(logs);
            Assert.StartsWith("TestOptions", log);
            Assert.Contains("\"SomeValue\": \"abc123\"", log);
        }

        [Fact]
        public async Task NonFormattableOptions_AreNotLogged()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddFormattableOptionsLogging();

            using ServiceProvider provider = services.BuildServiceProvider();

            var source = provider.GetRequiredService<IOptionsLoggingSource>();
            var factory = provider.GetRequiredService<IOptionsFactory<PlainOptions>>();

            factory.Create(Options.DefaultName);
            source.LogStream.Complete();

            IList<string> logs = new List<string>();
            while (await source.LogStream.OutputAvailableAsync(CancellationToken.None))
            {
                logs.Add(await source.LogStream.ReceiveAsync());
            }

            Assert.Empty(logs);
        }

        [Fact]
        public async Task RegisteredOptionsFormatter_IsUsedForLogging()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddFormattableOptionsLogging();
            services.AddSingleton<IOptionsFormatter<PlainOptions>, PlainOptionsFormatter>();

            using ServiceProvider provider = services.BuildServiceProvider();

            var source = provider.GetRequiredService<IOptionsLoggingSource>();
            var factory = provider.GetRequiredService<IOptionsFactory<PlainOptions>>();

            factory.Create(Options.DefaultName);
            source.LogStream.Complete();

            IList<string> logs = new List<string>();
            while (await source.LogStream.OutputAvailableAsync(CancellationToken.None))
            {
                logs.Add(await source.LogStream.ReceiveAsync());
            }

            string log = Assert.Single(logs);
            Assert.StartsWith("PlainOptions", log);
            Assert.Contains("\"Value\": \"default\"", log);
        }

        [Fact]
        public async Task MultipleCallsDoNotCauseDuplicateLogs()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddFormattableOptionsLogging();
            services.AddFormattableOptionsLogging();

            // Verify only one OptionsLoggingService is registered despite calling twice.
            int hostedServiceCount = services
                .Count(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(OptionsLoggingService));
            Assert.Equal(1, hostedServiceCount);

            using ServiceProvider provider = services.BuildServiceProvider();

            var source = provider.GetRequiredService<IOptionsLoggingSource>();
            var factory = provider.GetRequiredService<IOptionsFactory<TestOptions>>();

            factory.Create(Options.DefaultName);
            source.LogStream.Complete();

            IList<string> logs = new List<string>();
            while (await source.LogStream.OutputAvailableAsync(CancellationToken.None))
            {
                logs.Add(await source.LogStream.ReceiveAsync());
            }

            string log = Assert.Single(logs);
            Assert.StartsWith("TestOptions", log);
        }

        private class TestOptions : IOptionsFormatter
        {
            public string SomeValue { get; set; } = "abc123";

            public string Format()
            {
                JObject options = new JObject
                {
                    { nameof(SomeValue), SomeValue }
                };

                return options.ToString(Formatting.Indented);
            }
        }

        private class PlainOptions
        {
            public string Value { get; set; } = "default";
        }

        private class PlainOptionsFormatter : IOptionsFormatter<PlainOptions>
        {
            public string Format(PlainOptions options)
            {
                JObject obj = new JObject
                {
                    { nameof(options.Value), options.Value }
                };

                return obj.ToString(Formatting.Indented);
            }
        }
    }
}
