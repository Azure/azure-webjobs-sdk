using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class InvocationFilterEndToEndTests : IClassFixture<TestFixture>
    {
        private JobHostConfiguration _config;
        private JobHost _host;

        private const string TestArtifactPrefix = "e2etestmultiaccount";
        private const string Input = TestArtifactPrefix + "-input-%rnd%";
        private const string Output = TestArtifactPrefix + "-output-%rnd%";
        private const string InputTableName = TestArtifactPrefix + "tableinput%rnd%";
        private const string OutputTableName = TestArtifactPrefix + "tableinput%rnd%";
        private const string TestData = "﻿TestData";
        private const string Secondary = "SecondaryStorage";
        private readonly TestFixture _fixture;

        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        public InvocationFilterEndToEndTests(TestFixture fixture)
        {
            _config = new JobHostConfiguration()
            {
                TypeLocator = new FakeTypeLocator(typeof(AsyncChainEndToEndTests))
            };

            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_loggerProvider);
            _config.LoggerFactory = loggerFactory;
            _config.Aggregator.IsEnabled = false; // makes validation easier
            _config.TypeLocator = new FakeTypeLocator(typeof(StandardFilterTests), typeof(TestFunctionsInClass), typeof(ClassWithFunctionToTest), typeof(TestClassInterface));
            _host = new JobHost(_config);
            _fixture = fixture;
        }

        [Fact]
        public async Task TestInvocationLoggingFilter()
        {
            var method = typeof(StandardFilterTests).GetMethod("UseLoggingFilter", BindingFlags.Public | BindingFlags.Static);

            await _host.CallAsync(method, new { input = "Testing 123" });
            await Task.Delay(1000);

            // validate the before and after logging was passed
            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("Test executing!")));
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("Test executed!")));
        }

        [Fact]
        public async Task TestInvocationUserAuthenticationFilter()
        {
            var method = typeof(StandardFilterTests).GetMethod("UseUserAuthorizationFilter", BindingFlags.Public | BindingFlags.Static);

            await _host.CallAsync(method, new { input = "Testing 123" });
            await Task.Delay(1000);

            // Validate the authorized user was validated
            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("This is an authorized user!")));
        }

        [Fact]
        public async Task TestHTTPRequestFilter()
        {
            var method = typeof(StandardFilterTests).GetMethod("UseHTTPRequestFilter", BindingFlags.Public | BindingFlags.Static);
            HttpRequestMessage testHttpMessage = new HttpRequestMessage();

            await _host.CallAsync(method, new { input = "Testing 123", req = testHttpMessage });
            await Task.Delay(1000);

            // Validate the authorized user was validated
            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("Found the header!")));
        }

        [Fact]
        public async Task TestInvocationFalseUserAuthenticationFilter()
        {
            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Verbose);
            _config.Tracing.Tracers.Add(trace);
            var method = typeof(StandardFilterTests).GetMethod("TestFalseUserAuthorizationFilter", BindingFlags.Public | BindingFlags.Static);

            try
            {
                await _host.CallAsync(method, new { input = "Testing 123" });
            }
            catch { }

            // Validate the user was denied access
            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("This is an unauthorized user!")));
        }

        [Fact]
        public async Task TestFailingPreFilter()
        {
            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Verbose);
            _config.Tracing.Tracers.Add(trace);
            var method = typeof(StandardFilterTests).GetMethod("TestFailingPreFilter", BindingFlags.Public | BindingFlags.Static);

            try
            {
                await _host.CallAsync(method, new { input = "Testing 123" });
            }
            catch { }

            // Validate the before and after logging was passed
            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("Failing pre invocation!")));
        }

        [Fact]
        public async Task TestFailingPostFilter()
        {
            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Verbose);
            _config.Tracing.Tracers.Add(trace);
            var method = typeof(StandardFilterTests).GetMethod("TestFailingPostFilter", BindingFlags.Public | BindingFlags.Static);

            try
            {
                await _host.CallAsync(method, new { input = "Testing 123" });
            }
            catch (Exception e)
            {
                var message = e.Message;
            }

            // Validate the before and after logging was passed
            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("Failing post invocation!")));
        }

        [Fact]
        public async Task TestFailingFunction()
        {
            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Verbose);
            _config.Tracing.Tracers.Add(trace);
            var method = typeof(StandardFilterTests).GetMethod("TestFailingFunctionFilter", BindingFlags.Public | BindingFlags.Static);

            try
            {
                await _host.CallAsync(method, new { input = "Testing 123" });
            }
            catch (Exception e)
            {
                var message = e.Message;
            }

            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("Testing function fail")));

            string expectedName = $"{method.DeclaringType.FullName}.{method.Name}";

            // Validate TraceWriter
            // We expect 3 error messages total
            TraceEvent[] traceErrors = trace.Traces.Where(p => p.Level == TraceLevel.Error).ToArray();
            Assert.Equal(3, traceErrors.Length);

            // Ensure that all errors include the same exception, with function
            // invocation details           
            FunctionInvocationException functionException = traceErrors.First().Exception as FunctionInvocationException;
            Assert.NotNull(functionException);
            Assert.NotEqual(Guid.Empty, functionException.InstanceId);
            Assert.Equal(expectedName, functionException.MethodName);
            Assert.True(traceErrors.All(p => functionException == p.Exception));

            // Validate Logger
            // Logger only writes out a single log message (which includes the Exception).        
            logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Results).Single();
            var logMessage = logger.LogMessages.Single();
            var loggerException = logMessage.Exception as FunctionException;
            Assert.NotNull(loggerException);
            Assert.Equal(expectedName, loggerException.MethodName);
        }

        [Fact]
        public async Task TestInvokeFunctionFilter()
        {
            var method = typeof(StandardFilterTests).GetMethod("TestInvokeFunctionFilter", BindingFlags.Public | BindingFlags.Instance);

            string testValue = Guid.NewGuid().ToString();

            await _host.CallAsync(method, new { input = testValue });
            await Task.Delay(1000);

            // Read blob for the guid
            var blobReference = _fixture.OutputContainer.GetBlobReference("filterTest");
            await TestHelpers.Await(() =>
            {
                return blobReference.Exists();
            });

            string data;
            using (var memoryStream = new MemoryStream())
            {
                blobReference.DownloadToStream(memoryStream);
                memoryStream.Position = 0;
                using (var reader = new StreamReader(memoryStream, Encoding.Unicode))
                {
                    data = reader.ReadToEnd();
                }
            }
            Assert.Equal(testValue, data);
        }

        [Fact]
        public async Task TestPassingPropertiesInContext()
        {
            var method = typeof(StandardFilterTests).GetMethod("TestPropertiesInFunctionFilter", BindingFlags.Public | BindingFlags.Instance);
            string[] temp = { "passedProperty1" };

            await _host.CallAsync(method, new { input = temp });
            await Task.Delay(1000);

            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("filters!")));
        }

        [Fact]
        public async Task TestOrderOfExecution()
        {
            var method = typeof(StandardFilterTests).GetMethod("TestOrderWithFunctionFilters", BindingFlags.Public | BindingFlags.Static);

            await _host.CallAsync(method, new { input = "test" });
            await Task.Delay(1000);

            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("12")));
        }

        [Fact]
        public async Task TestMultipleInvokeFunctionFilters()
        {
            var method = typeof(StandardFilterTests).GetMethod("TestMultipleInvokeFunctionFilters", BindingFlags.Public | BindingFlags.Instance);

            string testValue = Guid.NewGuid().ToString();
            string testValue2 = Guid.NewGuid().ToString();
            string[] testValues = new string[2] {testValue, testValue2};


            await _host.CallAsync(method, new { input = testValues });
            await Task.Delay(1000);

            // Read both blobs for the guid
            var blobReference = _fixture.OutputContainer.GetBlobReference("filterTest");
            await TestHelpers.Await(() =>
            {
                return blobReference.Exists();
            });

            var blobReference2 = _fixture.OutputContainer.GetBlobReference("filterTest2");
            await TestHelpers.Await(() =>
            {
                return blobReference.Exists();
            });

            string data;
            using (var memoryStream = new MemoryStream())
            {
                blobReference.DownloadToStream(memoryStream);
                memoryStream.Position = 0;
                using (var reader = new StreamReader(memoryStream, Encoding.Unicode))
                {
                    data = reader.ReadToEnd();
                }
            }
            Assert.Equal(testValue, data);

            using (var memoryStream = new MemoryStream())
            {
                blobReference2.DownloadToStream(memoryStream);
                memoryStream.Position = 0;
                using (var reader = new StreamReader(memoryStream, Encoding.Unicode))
                {
                    data = reader.ReadToEnd();
                }
            }
            Assert.Equal(testValue2, data);

            // double check that the filter was exdecuted twice
            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("MyFirstFunction invoked!")));
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("MySecondFunction invoked!")));
        }
    }

    public class StandardFilterTests
    {
        [NoAutomaticTrigger]
        [TestLoggingFilter]
        public static void UseLoggingFilter(string input, ILogger logger)
        {
            logger.LogInformation("Test function invoked!");
        }

        [NoAutomaticTrigger]
        [TestUserAuthorizationFilter( AllowedUsers = "Admin")]
        public static void UseUserAuthorizationFilter(string input, ILogger logger)
        {
            logger.LogInformation("Test function invoked!");
        }

        [NoAutomaticTrigger]
        [HTTPRequestFilter]
        public static void UseHTTPRequestFilter(HttpRequestMessage req, string input, ILogger logger)
        {
            logger.LogInformation("Test function invoked!");
        }

        [NoAutomaticTrigger]
        [TestUserAuthorizationFilter(AllowedUsers = "Dave")]
        public static void TestFalseUserAuthorizationFilter(string input, ILogger logger)
        {
            logger.LogInformation("Test function invoked!");
        }

        [NoAutomaticTrigger]
        [TestFailingFilter(true)]
        public static void TestFailingPreFilter(string input, ILogger logger)
        {
            logger.LogInformation("Test function invoked!");
        }

        [NoAutomaticTrigger]
        [TestFailingFilter]
        public static void TestFailingPostFilter(string input, ILogger logger)
        {
            logger.LogInformation("Test function invoked!");
        }

        [NoAutomaticTrigger]
        [TestFailingFunctionFilter]
        public static void TestFailingFunctionFilter(string input, ILogger logger)
        {
            logger.LogInformation("Test function invoked!");
            throw new Exception("Testing function fail");
        }
            
        [NoAutomaticTrigger]
        [InvokeFunctionFilter("MyFunction")]
        public void TestInvokeFunctionFilter(string input, ILogger logger)
        {
            logger.LogInformation("Test function invoked!");
        }

        [NoAutomaticTrigger]
        [InvokeFunctionFilter(executingFilter: "MyFirstFunction")]
        [InvokeFunctionFilter(executedFilter: "MySecondFunction")]
        public void TestMultipleInvokeFunctionFilters(string[] input, ILogger logger)
        {
            logger.LogInformation("Test function invoked!");
        }

        [NoAutomaticTrigger]
        public void MyFunction(FunctionExecutingContext executingContext, [Blob("test/filterTest")] out string blob)
        {
            blob = (string)executingContext.Arguments["input"];
            executingContext.Logger.LogInformation("MyFunction invoked!");
        }

        [NoAutomaticTrigger]
        public static void MyFirstFunction(FunctionExecutingContext executingContext, [Blob("test/filterTest")] out string blob)
        {
            string[] stringArrayToUse = executingContext.Arguments["input"] as string[];
            blob = stringArrayToUse[0];
            executingContext.Logger.LogInformation("MyFirstFunction invoked!");
        }

        [NoAutomaticTrigger]
        public static void MySecondFunction(FunctionExecutedContext executedContext, [Blob("test/filterTest2")] out string blob)
        {
            string[] stringArrayToUse = executedContext.Arguments["input"] as string[];
            blob = stringArrayToUse[1];
            executedContext.Logger.LogInformation("MySecondFunction invoked!");
        }

        [NoAutomaticTrigger]
        [InvokeFunctionFilter(executingFilter: "PassProperty", executedFilter: "CatchProperty")]
        public void TestPropertiesInFunctionFilter(string[] input, ILogger logger)
        {
        }

        [NoAutomaticTrigger]
        public static void PassProperty(FunctionExecutingContext executingContext)
        {
            // Add the property to pass
            executingContext.Properties.Add("fakeproperty", "filters!");
            executingContext.Logger.LogInformation("Property was added to context");
        }

        [NoAutomaticTrigger]
        public static void CatchProperty(FunctionExecutedContext executedContext)
        {
            // Read the passed property
            executedContext.Logger.LogInformation((string)executedContext.Properties["fakeproperty"]);
            executedContext.Logger.LogInformation("Property from context was logged");
        }

        [NoAutomaticTrigger]
        [InvokeFunctionFilter(executingFilter: "AddToPropertyFirst", executedFilter: "AddToPropertyLast")]
        public static void TestOrderWithFunctionFilters(string input, ILogger logger)
        {
        }

        [NoAutomaticTrigger]
        public static void AddToPropertyFirst(FunctionExecutingContext executingContext)
        {
            // Add the property to pass
            executingContext.Properties.Add("fakeproperty", "1");
            executingContext.Logger.LogInformation("Property was added to context");
        }

        [NoAutomaticTrigger]
        public static void AddToPropertyLast(FunctionExecutedContext executedContext)
        {

            executedContext.Logger.LogInformation((string)executedContext.Properties["fakeproperty"] + "2");
            executedContext.Logger.LogInformation("Property from context was edited");
        }
    }

    public static class TestFunctionsInClass
    {
        [NoAutomaticTrigger]
        public static void TestAnotherFunction(string input, ILogger logger)
        {
            logger.LogInformation("TestAnotherFunction invoked!");
        }

        [NoAutomaticTrigger]
        public static void AnotherFunction(FunctionExecutingContext executingContext)
        {
            executingContext.Logger.LogInformation("AnotherFunction invoked!");
        }
    }

    public static class ClassWithFunctionToTest
    {
        [NoAutomaticTrigger]
        public static void AnotherFunction(FunctionExecutingContext executingContext)
        {
            executingContext.Logger.LogInformation("AnotherFunction invoked!");
        }
    }

    public class TestClassInterface : IFunctionInvocationFilter
    {
        [NoAutomaticTrigger]
        [InvokeFunctionFilter(executingFilter: "TestClassInterface")]
        public void TestInterface(string input, ILogger logger)
        {
            logger.LogInformation("TestInterface invoked!");
        }

        public Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
        {
            executingContext.Logger.LogInformation("Class.OnExecutingAsync invoked!");
            return Task.CompletedTask;
        }

        public Task OnExecutedAsync(FunctionExecutedContext executedContext, CancellationToken cancellationToken)
        {
            executedContext.Logger.LogInformation("Class.OnExecutedAsync invoked!");
            return Task.CompletedTask;
        }
    }

    public class TestLoggingFilter : InvocationFilterAttribute
    {
        public override Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
        {
            executingContext.Logger.LogInformation("Test executing!");
            return Task.CompletedTask;
        }

        public override Task OnExecutedAsync(FunctionExecutedContext executedContext, CancellationToken cancellationToken)
        {
            executedContext.Logger.LogInformation("Test executed!");
            return Task.CompletedTask;
        }
    }

    public class TestUserAuthorizationFilter : InvocationFilterAttribute
    {
        public string AllowedUsers { get; set; }

        public override Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
        {
            executingContext.Logger.LogInformation("Test executing!");

            if (!AllowedUsers.Contains("Admin"))
            {
                executingContext.Logger.LogInformation("This is an unauthorized user!");
                throw new Exception("Not Allowing Unauthorized Users!");
            }

            executingContext.Logger.LogInformation("This is an authorized user!");
            return Task.CompletedTask;
        }
    }

    public class HTTPRequestFilter : InvocationFilterAttribute
    {
        public HttpRequestMessage httpRequestToValidate { get; set; }

        public override Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
        {
            executingContext.Logger.LogInformation("Test executing!");

            IReadOnlyDictionary<string, object> arguments = executingContext.Arguments;
            var testValues = string.Join(",", arguments.Values.ToArray());

            // Check headers (I'm sure there's a better way to check this
            if (testValues.Contains("Headers"))
            {
                executingContext.Logger.LogInformation("Found the header!");
                // Perform a validation on the headers
            }

            return Task.CompletedTask;
        }
    }

    public class TestFailingFilter : InvocationFilterAttribute
    {
        bool failPreInvocation = false;

        public TestFailingFilter(bool input = false)
        {
            failPreInvocation = input;
        }

        public override Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
        {
            if ( failPreInvocation == true )
            {
                executingContext.Logger.LogInformation("Failing pre invocation!");
                throw new Exception("Failing on purpose!");
            }

            return Task.CompletedTask;
        }

        public override Task OnExecutedAsync(FunctionExecutedContext executedContext, CancellationToken cancellationToken)
        {
            if (failPreInvocation == true)
            {
                return Task.CompletedTask;
            }

            executedContext.Logger.LogInformation("Failing post invocation!");
            throw new Exception("Failing on purpose!");
        }
    }

    public class TestFailingFunctionFilter : InvocationFilterAttribute
    {
        public override Task OnExecutedAsync(FunctionExecutedContext executedContext, CancellationToken cancellationToken)
        {
            executedContext.Logger.LogInformation("The function failed!");
            executedContext.Logger.LogInformation(executedContext.Result.Exception.ToString());

            return Task.CompletedTask;
        }
    }

    internal class TestNameResolver : RandomNameResolver
    {
        public override string Resolve(string name)
        {
            if (name == "test_account")
            {
                return "SecondaryStorage";
            }
            return base.Resolve(name);
        }
    }

    public class TestFixture : IDisposable
    {
        public TestFixture()
        {
            RandomNameResolver nameResolver = new TestNameResolver();
            JobHostConfiguration hostConfiguration = new JobHostConfiguration()
            {
                NameResolver = nameResolver,
                TypeLocator = new FakeTypeLocator(typeof(MultipleStorageAccountsEndToEndTests)),
            };
            Config = hostConfiguration;

            var account = CloudStorageAccount.Parse(hostConfiguration.StorageConnectionString);

            CloudBlobClient blobClient1 = account.CreateCloudBlobClient();
            OutputContainer = blobClient1.GetContainerReference("test");

            Host = new JobHost(hostConfiguration);
            Host.Start();
        }

        public JobHost Host
        {
            get;
            private set;
        }

        public JobHostConfiguration Config
        {
            get;
            private set;
        }

        public CloudBlobContainer OutputContainer { get; private set; }

        public void Dispose()
        {
            ((IDisposable)Host).Dispose();
        }
    }
}
