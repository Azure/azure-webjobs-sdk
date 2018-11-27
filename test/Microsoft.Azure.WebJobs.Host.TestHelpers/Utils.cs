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

namespace Microsoft.Azure.WebJobs.Host.TestHelpers
{
    public static class TestUtils
    {
        public static IServiceCollection AddSingletonIfNotNull<T>(this IServiceCollection services, T instance) where T : class
        {
            if (instance != null)
            {
                services.AddSingleton<T>(instance);
            }

            return services;
        }

        // Test error if not reached within a timeout 
        public static Task<TResult> AwaitWithTimeout<TResult>(this TaskCompletionSource<TResult> taskSource)
        {
            return taskSource.Task;
        }

        // Test error if not reached within a timeout 
        public static TResult AwaitWithTimeout<TResult>(this Task<TResult> taskSource)
        {
            Await(() => taskSource.IsCompleted).Wait();
            return taskSource.Result;
        }

        public static async Task Await(Func<Task<bool>> condition, int timeout = 60 * 1000, int pollingInterval = 50, bool throwWhenDebugging = false, Func<string> userMessageCallback = null)
        {
            DateTime start = DateTime.Now;
            while (!await condition())
            {
                await Task.Delay(pollingInterval);

                bool shouldThrow = !Debugger.IsAttached || (Debugger.IsAttached && throwWhenDebugging);
                if (shouldThrow && (DateTime.Now - start).TotalMilliseconds > timeout)
                {
                    string error = "Condition not reached within timeout.";
                    if (userMessageCallback != null)
                    {
                        error += " " + userMessageCallback();
                    }
                    throw new ApplicationException(error);
                }
            }
        }

        public static async Task Await(Func<bool> condition, int timeout = 60 * 1000, int pollingInterval = 50, bool throwWhenDebugging = false, Func<string> userMessageCallback = null)
        {
            await Await(() => Task.FromResult(condition()), timeout, pollingInterval, throwWhenDebugging, userMessageCallback);
        }

        public static void WaitOne(WaitHandle handle, int timeout = 60 * 1000)
        {
            bool ok = handle.WaitOne(timeout);
            if (!ok)
            {
                // timeout. Event not signaled in time. 
                throw new ApplicationException("Condition not reached within timeout.");
            }
        }

        public static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                field = target.GetType().GetField($"<{fieldName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            field.SetValue(target, value);
        }

        public static T New<T>()
        {
            var constructor = typeof(T).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);
            return (T)constructor.Invoke(null);
        }

        // Test that we get an indexing error (FunctionIndexingException)  
        // functionName - the function name that has the indexing error. 
        // expectedErrorMessage - inner exception's message with details.
        // Invoking func() should cause an indexing error. 
        public static void AssertIndexingError(Action func, string functionName, string expectedErrorMessage)
        {
            try
            {
                func(); // expected indexing error
            }
            catch (FunctionIndexingException e)
            {
                Assert.Equal("Error indexing method '" + functionName + "'", e.Message);
                Assert.StartsWith(expectedErrorMessage, e.InnerException.Message);
                return;
            }
            Assert.True(false, "Invoker should have failed");
        }

        public static IHostBuilder ConfigureTestLogger(this IHostBuilder builder)
        {
            return builder.ConfigureLogging(logging =>
            {
                logging.AddProvider(new TestLoggerProvider());
            });
        }

        public static IHostBuilder ConfigureTypeLocator(this IHostBuilder builder, params Type[] types)
        {
            return builder.ConfigureServices(services =>
            {
                services.AddSingleton<ITypeLocator>(new FakeTypeLocator(types));
            });
        }

        public static TestLoggerProvider GetTestLoggerProvider(this IHost host)
        {
            return host.Services.GetServices<ILoggerProvider>().OfType<TestLoggerProvider>().Single();
        }

        public static TExtension GetExtension<TExtension>(this IHost host)
        {
            return host.Services.GetServices<IExtensionConfigProvider>().OfType<TExtension>().SingleOrDefault();
        }

        public static async Task CallAsync<T>(this JobHost host, string methodName, object arguments)
        {
            await host.CallAsync(typeof(T).GetMethod(methodName), arguments);
        }

        public static async Task CallAsync<T>(this JobHost host, string methodName)
        {
            await host.CallAsync(typeof(T).GetMethod(methodName));
        }

        public static TOptions GetOptions<TOptions>(this IHost host) where TOptions : class, new()
        {
            return host.Services.GetService<IOptions<TOptions>>().Value;
        }

        public static IJobHostMetadataProvider CreateMetadataProvider(this IHost host)
        {
            return host.Services.GetService<IJobHostMetadataProvider>();
        }

        public static List<string> GetAssemblyReferences(Assembly assembly)
        {
            var assemblyRefs = assembly.GetReferencedAssemblies();
            var names = (from assemblyRef in assemblyRefs
                         orderby assemblyRef.Name.ToLowerInvariant()
                         select assemblyRef.Name).ToList();
            return names;
        }

        public static void AssertPublicTypes(IEnumerable<string> expected, Assembly assembly)
        {
            var publicTypes = (assembly.GetExportedTypes()
                .Select(type => type.Name)
                .OrderBy(n => n));

            AssertPublicTypes(expected.ToArray(), publicTypes.ToArray());
        }

        public static void AssertPublicTypes(string[] expected, string[] actual)
        {
            var newlyIntroducedPublicTypes = actual.Except(expected).ToArray();

            if (newlyIntroducedPublicTypes.Length > 0)
            {
                string message = string.Format("Found {0} unexpected public type{1}: \r\n{2}",
                    newlyIntroducedPublicTypes.Length,
                    newlyIntroducedPublicTypes.Length == 1 ? "" : "s",
                    string.Join("\r\n", newlyIntroducedPublicTypes));
                Assert.True(false, message);
            }

            var missingPublicTypes = expected.Except(actual).ToArray();

            if (missingPublicTypes.Length > 0)
            {
                string message = string.Format("missing {0} public type{1}: \r\n{2}",
                    missingPublicTypes.Length,
                    missingPublicTypes.Length == 1 ? "" : "s",
                    string.Join("\r\n", missingPublicTypes));
                Assert.True(false, message);
            }
        }

        public static IDictionary<string, string> CreateInMemoryCollection()
        {
            return new Dictionary<string, string>();
        }

        public static IDictionary<string, string> AddSetting(this IDictionary<string, string> dict, string name, string value)
        {
            dict.Add(name, value);
            return dict;
        }

        public static IConfiguration BuildConfiguration(this IDictionary<string, string> dict)
        {
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }
    }
}
