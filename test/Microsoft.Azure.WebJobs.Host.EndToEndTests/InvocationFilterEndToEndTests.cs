using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class InvocationFilterEndToEndTests
    {
        private JobHostConfiguration _config;
        private JobHost _host;

        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        public InvocationFilterEndToEndTests()
        {
            _config = new JobHostConfiguration()
            {
                TypeLocator = new FakeTypeLocator(typeof(AsyncChainEndToEndTests))
            };

            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_loggerProvider);
            _config.LoggerFactory = loggerFactory;
            _config.Aggregator.IsEnabled = false; // makes validation easier
            _config.TypeLocator = new FakeTypeLocator(typeof(TestFunctions));
            _host = new JobHost(_config);
        }

        [Fact]
        public async Task TestInvocationLoggingFilter()
        {
            var method = typeof(TestFunctions).GetMethod("UseLoggingFilter", BindingFlags.Public | BindingFlags.Static);

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
            var method = typeof(TestFunctions).GetMethod("UseUserAuthorizationFilter", BindingFlags.Public | BindingFlags.Static);

            await _host.CallAsync(method, new { input = "Testing 123" });
            await Task.Delay(1000);

            // Validate the before and after logging was passed
            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("This is an authorized user!")));
        }

        [Fact]
        public async Task TestInvocationFalseUserAuthenticationFilter()
        {
            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Verbose);
            _config.Tracing.Tracers.Add(trace);
            var method = typeof(TestFunctions).GetMethod("TestFalseUserAuthorizationFilter", BindingFlags.Public | BindingFlags.Static);

            try
            {
                await _host.CallAsync(method, new { input = "Testing 123" });
            }
            catch { }

            // Validate the before and after logging was passed
            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("This is an unauthorized user!")));
        }

        [Fact]
        public async Task TestFailingPreFilter()
        {
            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Verbose);
            _config.Tracing.Tracers.Add(trace);
            var method = typeof(TestFunctions).GetMethod("TestFailingPreFilter", BindingFlags.Public | BindingFlags.Static);

            try
            {
                await _host.CallAsync(method, new { input = "Testing 123" });
            }
            catch { }

            // Validate the before and after logging was passed
            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("Failing pre invocation!")));

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
        public async Task TestFailingPostFilter()
        {
            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Verbose);
            _config.Tracing.Tracers.Add(trace);
            var method = typeof(TestFunctions).GetMethod("TestFailingPostFilter", BindingFlags.Public | BindingFlags.Static);

            try
            {
                await _host.CallAsync(method, new { input = "Testing 123" });
            }
            catch { }

            // Validate the before and after logging was passed
            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("Failing post invocation!")));

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

        public class TestFunctions
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
        }

        public class TestLoggingFilter : InvocationFilterAttribute
        {
            public override Task OnPreFunctionInvocation(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
            {
                executingContext.Logger.LogInformation("Test executing!");
                return Task.CompletedTask;
            }

            public override Task OnPostFunctionInvocation(FunctionExecutedContext executedContext, CancellationToken cancellationToken)
            {
                executedContext.Logger.LogInformation("Test executed!");
                return Task.CompletedTask;
            }
        }

        public class TestUserAuthorizationFilter : InvocationFilterAttribute
        {
            public string AllowedUsers { get; set; }

            public override Task OnPreFunctionInvocation(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
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

        public class TestValidationFilter : InvocationFilterAttribute
        {
            public string textToValidate { get; set; }

            public TestValidationFilter(string inputText)
            {
                textToValidate = inputText;
            }

            public override Task OnPreFunctionInvocation(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
            {
                executingContext.Logger.LogInformation("Test executing!");

                if (!textToValidate.Contains("azurefunctions"))
                {
                    executingContext.Logger.LogInformation("The input is incorrect!");
                }

                executingContext.Logger.LogInformation("The input is correct!");

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

            public override Task OnPreFunctionInvocation(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
            {
                if ( failPreInvocation == true )
                {
                    executingContext.Logger.LogInformation("Failing pre invocation!");
                    throw new Exception("Failing on purpose!");
                }

                return Task.CompletedTask;
            }

            public override Task OnPostFunctionInvocation(FunctionExecutedContext executedContext, CancellationToken cancellationToken)
            {
                executedContext.Logger.LogInformation("Failing post invocation!");
                throw new Exception("Failing on purpose!");
            }
        }
    }
}
