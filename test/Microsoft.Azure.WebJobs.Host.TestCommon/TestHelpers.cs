// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.FunctionalTests.TestDoubles;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public static class TestHelpers
    {
        /// <summary>
        /// Helper that builds a test host to configure the options type specified.
        /// </summary>
        /// <typeparam name="TOptions">The options type to configure.</typeparam>
        /// <param name="configure">Delegate used to configure the target extension.</param>
        /// <param name="configValues">Set of test configuration values to apply.</param>
        /// <returns></returns>
        public static TOptions GetConfiguredOptions<TOptions>(Action<IWebJobsBuilder> configure, Dictionary<string, string> configValues) where TOptions : class, new()
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<TestProgram>(b =>
                {
                    configure(b);
                })
                .ConfigureAppConfiguration(cb =>
                {
                    cb.AddInMemoryCollection(configValues);
                })
                .Build();

            TOptions options = host.Services.GetRequiredService<IOptions<TOptions>>().Value;
            return options;
        }

        public static IHostBuilder ConfigureDefaultTestHost(this IHostBuilder builder, params Type[] types)
        {
            return builder.ConfigureDefaultTestHost(b => { }, types);
        }

        public static IHostBuilder ConfigureDefaultTestHost(this IHostBuilder builder, Action<IWebJobsBuilder> configureWebJobs, params Type[] types)
        {
            return builder.ConfigureWebJobs(configureWebJobs)
                .ConfigureAppConfiguration(c =>
                {
                    c.AddTestSettings();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ITypeLocator>(new FakeTypeLocator(types));

                    // Register this to fail a test if a background exception is thrown
                    services.AddSingleton<IWebJobsExceptionHandlerFactory, TestExceptionHandlerFactory>();
                })
                .ConfigureTestLogger();
        }

        public static IHostBuilder ConfigureDefaultTestHost<TProgram>(this IHostBuilder builder,
           TProgram instance, Action<IWebJobsBuilder> configureWebJobs)
        {
            return builder.ConfigureDefaultTestHost(configureWebJobs, typeof(TProgram))
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IJobHost, JobHost<TProgram>>();

                    services.AddSingleton<IJobActivator>(new FakeActivator(instance));
                });
        }

        public static IHostBuilder ConfigureDefaultTestHost<TProgram>(this IHostBuilder builder)
        {
            return builder.ConfigureDefaultTestHost(o => { }, typeof(TProgram))
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IJobHost, JobHost<TProgram>>();
                });
        }

        public static IHostBuilder ConfigureDefaultTestHost<TProgram>(this IHostBuilder builder, Action<IWebJobsBuilder> configureWebJobs,
            INameResolver nameResolver = null, IJobActivator activator = null)
        {
            return builder.ConfigureDefaultTestHost(configureWebJobs, typeof(TProgram))
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IJobHost, JobHost<TProgram>>();

                    if (nameResolver != null)
                    {
                        services.AddSingleton<INameResolver>(nameResolver);
                    }

                    if (activator != null)
                    {
                        services.AddSingleton<IJobActivator>(activator);
                    }
                });
        }

        public static IWebJobsBuilder ConfigureCatchFailures<TResult>(
            this IWebJobsBuilder builder,
            TaskCompletionSource<TResult> src,
            bool signalOnFirst,
            IEnumerable<string> ignoreFailureFunctions)
        {
            var logger = new ExpectManualCompletionFunctionInstanceLogger<TResult>(
                src,
                signalOnFirst,
                ignoreFailureFunctions);

            builder.Services.AddSingleton<IFunctionInstanceLogger>(logger);

            return builder;
        }

        public static JobHost GetJobHost(this IHost host)
        {
            return host.Services.GetService<IJobHost>() as JobHost;
        }

        public static JobHost<TProgram> GetJobHost<TProgram>(this IHost host)
        {
            return host.Services.GetService<IJobHost>() as JobHost<TProgram>;
        }
    }

    public class TestProgram
    {
    }
}
