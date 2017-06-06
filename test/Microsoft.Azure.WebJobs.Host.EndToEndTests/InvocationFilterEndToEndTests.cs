using System;
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
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("Test Executing!")));
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("Test Executed!")));
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
            var method = typeof(TestFunctions).GetMethod("TestFalseUserAuthorizationFilter", BindingFlags.Public | BindingFlags.Static);

            await _host.CallAsync(method, new { input = "Testing 123" });
            await Task.Delay(1000);

            // Validate the before and after logging was passed
            var logger = _loggerProvider.CreatedLoggers.Where(l => l.Category == LogCategories.Executor).Single();
            Assert.NotNull(logger.LogMessages.SingleOrDefault(p => p.FormattedMessage.Contains("This is an unauthorized user!")));
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
        }

        public class TestLoggingFilter : InvocationFilterAttribute
        {
            public override Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
            {
                executingContext.Logger.LogInformation("Test Executing!");
                return Task.CompletedTask;
            }

            public override Task OnActionExecuted(FunctionExecutedContext executedContext, CancellationToken cancellationToken)
            {
                executedContext.Logger.LogInformation("Test Executed!");
                return Task.CompletedTask;
            }
        }

        public class TestUserAuthorizationFilter : InvocationFilterAttribute
        {
            public string AllowedUsers { get; set; }

            public override Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
            {
                executingContext.Logger.LogInformation("Test Executing!");

                if (!AllowedUsers.Contains("Admin"))
                {
                    executingContext.Logger.LogInformation("This is an unauthorized user!");
                    // throw new Exception("Not Allowing Unauthorized Users!");
                }

                executingContext.Logger.LogInformation("This is an authorized user!");

                return Task.CompletedTask;
            }
        }
    }
}
