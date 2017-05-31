// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Executors;
using Xunit;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using Moq;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Executors
{
    public class TriggeredFunctionExecutorTests
    {
        // Test ITriggeredFunctionExecutorWithHook
        [Fact]
        public async Task TestHook()
        {
            StringBuilder sb = new StringBuilder();

            var descr = new FunctionDescriptor();

            // IFunctionExecutor just passes through to Invoker.
            var mockExecutor = new Mock<IFunctionExecutor>();
            mockExecutor.Setup(m => m.TryExecuteAsync(It.IsAny<IFunctionInstance>(), It.IsAny<CancellationToken>())).
                Returns<IFunctionInstance, CancellationToken>((x, y) =>
                {
                    sb.Append("2>");
                    x.Invoker.InvokeAsync(null).Wait();
                    sb.Append("<6");
                    return Task.FromResult<IDelayedException>(null);
                });
            IFunctionExecutor executor = mockExecutor.Object;

            var mockInvoker = new Mock<IFunctionInvoker>();
            mockInvoker.Setup(m => m.InvokeAsync(null)).Returns(() =>
            {
                sb.Append("4");
                return Task.CompletedTask;
            }
                );
            IFunctionInvoker innerInvoker = mockInvoker.Object;

            IFunctionInstance inner = new FunctionInstance(Guid.NewGuid(), null, ExecutionReason.HostCall, null, innerInvoker, null);

            var mockInstanceFactory = new Mock<ITriggeredFunctionInstanceFactory<int>>();
            mockInstanceFactory.Setup(m => m.Create(It.IsAny<int>(), null)).Returns(inner);
            ITriggeredFunctionInstanceFactory<int> instanceFactory = mockInstanceFactory.Object;

            var trigger = new TriggeredFunctionExecutor<int>(descr, executor, instanceFactory);

            var trigger2 = (ITriggeredFunctionExecutorWithHook)trigger;

            

            Func<Func<Task>, Task> hook = async (x) => {
                sb.Append("3>");
                await x();
                sb.Append("<5");
            };

            sb.Append("1>");
            await trigger2.TryExecuteAsync(new TriggeredFunctionData { TriggerValue = 123 }, CancellationToken.None, hook);
            sb.Append("<7");

            Assert.Equal("1>2>3>4<5<6<7", sb.ToString());
        }
    }
}
