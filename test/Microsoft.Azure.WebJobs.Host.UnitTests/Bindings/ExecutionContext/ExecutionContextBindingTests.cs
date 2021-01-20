// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Bindings
{
    public class ExecutionContextBindingTests
    {
        [Fact]
        public async Task CanBindExecutionContext()
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<CoreTestJobs>(b =>
                {
                    b.AddExecutionContextBinding();
                })
                .Build();

            var jobHost = host.GetJobHost<CoreTestJobs>();

            string methodName = nameof(CoreTestJobs.ExecutionContext);
            await jobHost.CallAsync(typeof(CoreTestJobs).GetMethod(methodName));

            ExecutionContext result = CoreTestJobs.Context;
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.InvocationId);
            Assert.Equal(methodName, result.FunctionName);
            Assert.Equal(Environment.CurrentDirectory, result.FunctionDirectory);
        }

        [Fact]
        public async Task SetAppDirectory()
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<CoreTestJobs>(b =>
                {
                    b.AddExecutionContextBinding(o =>
                    {
                        o.AppDirectory = @"z:\home";
                    });
                })
                .Build();

            var jobHost = host.GetJobHost<CoreTestJobs>();

            await jobHost.CallAsync("myfunc");

            ExecutionContext result = CoreTestJobs.Context;
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.InvocationId);
            Assert.Equal("myfunc", result.FunctionName);
            Assert.Equal(@"z:\home\myfunc", result.FunctionDirectory);
            Assert.Equal(@"z:\home", result.FunctionAppDirectory);
        }

        [Fact]
        public async Task SetRetryContext()
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<RetryTestJob>(b =>
                {
                    b.AddExecutionContextBinding(o =>
                    {
                        o.AppDirectory = @"z:\home";
                    });
                })
                .Build();

            var jobHost = host.GetJobHost<RetryTestJob>();

            await jobHost.CallAsync("myfunc");
            ExecutionContext result = RetryTestJob.Context;
            Assert.NotNull(result);
            Assert.Equal(result.RetryContext.RetryCount, 3);
        }

        public class CoreTestJobs
        {
            public static ExecutionContext Context { get; set; }

            [NoAutomaticTrigger]
            public static void ExecutionContext(ExecutionContext context)
            {
                Context = context;
            }

            [NoAutomaticTrigger]
            [FunctionName("myfunc")]
            public static void ExecutionContext2(ExecutionContext context)
            {
                Context = context;
            }
        }

        public class RetryTestJob
        {
            public static ExecutionContext Context { get; set; }

            [FixedDelayRetry(3, "00:00:01")]
            [NoAutomaticTrigger]
            [FunctionName("myfunc")]
            public static void ExecutionContext(ExecutionContext context)
            {
                if (context.RetryContext == null)
                {
                    throw new Exception("0");
                }

                Context = context;

                Assert.Equal(context.RetryContext.MaxRetryCount, 3);

                if (context.RetryContext.RetryCount != 0)
                {
                    Assert.Equal(context.RetryContext.RetryCount - 1, int.Parse(context.RetryContext.Exception.InnerException.Message));
                }

                if (context.RetryContext.RetryCount != 3)
                {
                    throw new Exception(context.RetryContext.RetryCount.ToString());
                }
            }
        }
    }
}
