// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class RetryManagerTests
    {
        private const int Timeout = 30000;
        public static EventWaitHandle EventWait;

        [Fact]
        public async Task ThrowException_AllRetriesExhausted()
        {
            var logs = await ExecuteTest<ThrowException_AllRetriesExhaustedClass>();

            Assert.Equal(3, logs.Where(x => x.Contains("New linear retry attempt")).Count());
            Assert.Single(logs.Where(x => x.Contains("All retries have been exhausted")));
        }

        [Fact]
        public async Task ThrowException_Successed()
        {
            var logs = await ExecuteTest<ThrowException_SuccessedClass>();

            Assert.Equal(3, logs.Where(x => x.Contains("New linear retry attempt")).Count());
        }

        [Fact]
        public async Task ThrowException_Successed_ClassAttribute()
        {
            var logs = await ExecuteTest<ThrowException_SuccessedClass_ClassAttribute>();

            Assert.Equal(3, logs.Where(x => x.Contains("New linear retry attempt")).Count());
        }

        [Fact]
        public async Task ThrowException_AllRetriesExhausted_Exponential()
        {
            Stopwatch watch = new Stopwatch();

            watch.Start();
            var logs = await ExecuteTest<ThrowException_AllRetriesExhaustedExponentialBackoffClass>();
            watch.Stop();

            Assert.True(watch.ElapsedMilliseconds > 10000);
            Assert.Equal(3, logs.Where(x => x.Contains("New exponential retry attempt")).Count());
            Assert.Single(logs.Where(x => x.Contains("All retries have been exhausted")));
        }

        [Fact]
        public async Task ThrowException_Infinite()
        {
            Stopwatch watch = new Stopwatch();

            watch.Start();
            var logs = await ExecuteTest<ThrowException_InfiniteClass>(false);
            watch.Stop();

            Assert.True(watch.ElapsedMilliseconds > 30000);
        }

        [Fact]
        public async Task ReturnRetry_AllRetriesExhausted()
        {
            var logs = await ExecuteTest<RetryReturn_AllRetriesExhaustedClass>();

            Assert.Single(logs.Where(x => x.Contains("Function code returned retry settings")));
            Assert.Equal(3, logs.Where(x => x.Contains("New linear retry attempt")).Count());
            Assert.Single(logs.Where(x => x.Contains("All retries have been exhausted")));
        }

        [Fact]
        public async Task RetryReturn_Successed()
        {
            var logs = await ExecuteTest<RetryReturn_SuccessedClass>();

            Assert.Single(logs.Where(x => x.Contains("Function code returned retry settings")));
            Assert.Equal(3, logs.Where(x => x.Contains("New linear retry attempt")).Count());
            Assert.DoesNotContain("All retries have been exhausted", logs);
        }

        [Fact]
        public async Task ReturnRetry_Infinite()
        {
            Stopwatch watch = new Stopwatch();

            watch.Start();
            var logs = await ExecuteTest<ReturnRetry_InfiniteClass>(false);
            watch.Stop();

            Assert.True(watch.ElapsedMilliseconds > 30000);
        }

        [Fact]
        public async Task ReturnRetry_Wins()
        {
            var logs = await ExecuteTest<ReturnRetry_WinsClass>();

            Assert.Equal(6, logs.Where(x => x.Contains("New linear retry attempt")).Count());
            Assert.Single(logs.Where(x => x.Contains("All retries have been exhausted")));
        }

        private async Task<string[]> ExecuteTest<T>(bool checkForTimeout = true)
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            TestLoggerProvider loggerProvider = new TestLoggerProvider();
            loggerFactory.AddProvider(loggerProvider);

            EventWait = new ManualResetEvent(false);
            var ext = new RetryTestExtension();

            IHost host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ILoggerFactory>(loggerFactory);
                })
                .ConfigureDefaultTestHost<T>(b =>
                {
                    b.AddExecutionContextBinding()
                    .AddExtension(ext);
                })
                .Build();
            await host.StartAsync();

            ext.OnEvent.Invoke(this, new EventArgs());

            bool result = EventWait.WaitOne(Timeout);
            if (checkForTimeout)
            {
                Assert.True(result);
            }

            // wait all logs tp flush
            await Task.Delay(3000);

            await host.StopAsync();
            return await Task.FromResult(loggerProvider.GetAllLogMessages().Where(x => x.FormattedMessage != null).Select(x => x.FormattedMessage).ToArray());
        }

        public class ThrowException_AllRetriesExhaustedClass
        {
            [Retry(3, "00:00:01")]
            public void Trigger([RestryTestAttribute] string message, ExecutionContext context)
            {
                if (context.RetryCount == 3)
                {
                    EventWait.Set();
                }
                throw new Exception("Test");
            }
        }

        public class ThrowException_AllRetriesExhaustedExponentialBackoffClass
        {
            [Retry(3, null, true)]
            public void Trigger([RestryTestAttribute] string message, ExecutionContext context)
            {
                if (context.RetryCount == 3)
                {
                    EventWait.Set();
                }
                throw new Exception("Test");
            }
        }

        public class ThrowException_InfiniteClass
        {

            [Retry(-1, "00:00:01")]
            public void Trigger([RestryTestAttribute] string message, ExecutionContext context)
            {
                throw new Exception("Test");
            }
        }

        public class ReturnRetry_InfiniteClass
        {
            [Retry(3, "00:00:01")]
            public Task<RetryResult> Trigger([RestryTestAttribute] string message, ExecutionContext context)
            {
                if (context.RetryCount == -1)
                {
                    return Task.FromResult(new RetryResult(-1, "00:00:01"));
                }
                throw new Exception("Test");
            }
        }

        public class RetryReturn_AllRetriesExhaustedClass
        {
            public static bool RetryReturned = false;

            public RetryResult Trigger([RestryTestAttribute] string message, ExecutionContext context)
            {
                if (!RetryReturned && context.RetryCount == 0)
                {
                    RetryReturned = true;
                    return new RetryResult(3, "00:00:01");
                }
                if (context.RetryCount == 3)
                {
                    EventWait.Set();
                }
                throw new Exception("Test");
            }
        }

        public class ThrowException_SuccessedClass
        {
            [Retry(10, "00:00:01")]
            public void Trigger([RestryTestAttribute] string message, ExecutionContext context)
            {
                if (context.RetryCount == 3)
                {
                    EventWait.Set();
                    return;
                }
                throw new InvalidOperationException("Test");
            }
        }

        [Retry(10, "00:00:01")]
        public class ThrowException_SuccessedClass_ClassAttribute
        {
            public void Trigger([RestryTestAttribute] string message, ExecutionContext context)
            {
                if (context.RetryCount == 3)
                {
                    EventWait.Set();
                    return;
                }
                throw new InvalidOperationException("Test");
            }
        }

        public class RetryReturn_SuccessedClass
        {
            public static bool RetryReturned = false;

            public RetryResult Trigger([RestryTestAttribute] string message, ExecutionContext context)
            {
                if (!RetryReturned && context.RetryCount == 0)
                {
                    RetryReturned = true;
                    return new RetryResult(10, "00:00:01");
                }
                if (context.RetryCount == 3)
                {
                    EventWait.Set();
                    return null;
                }
                throw new InvalidOperationException("Test");
            }
        }

        public class ReturnRetry_WinsClass
        {
            public static bool RetryReturned = false;

            [Retry(10, "00:00:01")]
            public RetryResult Trigger([RestryTestAttribute] string message, ExecutionContext context)
            {
                if (!RetryReturned && context.RetryCount == 1)
                {
                    RetryReturned = true;
                    return new RetryResult(5, "00:00:01");
                }
                if (RetryReturned && context.RetryCount == 5)
                {
                    EventWait.Set();
                }
                throw new Exception("Test");
            }
        }

        // Bind to a regular async collector (output) binding,
        [Binding]
        public class RestryTestAttributeAttribute : Attribute
        {
        }

        class RetryTestExtension : IExtensionConfigProvider, ITriggerBindingProvider
        {
            public EventHandler OnEvent { get; set; }

            public RetryTestExtension()
            {
            }

            public void Initialize(ExtensionConfigContext context)
            {
                context.AddBindingRule<RestryTestAttributeAttribute>().
                    BindToTrigger(this);
            }

            Task<ITriggerBinding> ITriggerBindingProvider.TryCreateAsync(TriggerBindingProviderContext context)
            {
                RestryTestAttributeAttribute attribute = context.Parameter.GetCustomAttribute<RestryTestAttributeAttribute>(inherit: false);

                if (attribute == null)
                {
                    return Task.FromResult<ITriggerBinding>(null);
                }

                return Task.FromResult<ITriggerBinding>(new RetryTestTrigger(this));
            }

            class RetryTestTrigger : ITriggerBinding
            {
                RetryTestExtension _parent;

                public RetryTestTrigger(RetryTestExtension parent)
                {
                    _parent = parent;
                }
                public Type TriggerValueType => typeof(string);

                public IReadOnlyDictionary<string, Type> BindingDataContract => new Dictionary<string, Type>
                {
                    { "$return", typeof(RetryResult).MakeByRefType() }
                };

                public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
                {
                    var inputValueProvider = new RetryTestValueProvider(_parent) { Value = value };
                    var returnValueProvider = new RetryTestReturnValueProvider(_parent) { Value = "return" };
                    var bindingData = new Dictionary<string, object>();
                    var triggerData = new TriggerData(inputValueProvider, bindingData)
                    {
                        ReturnValueProvider = returnValueProvider
                    };
                    return Task.FromResult<ITriggerData>(triggerData);
                }

                public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
                {
                    return Task.FromResult<IListener>(new RetryTestListener(context, _parent));
                }

                public ParameterDescriptor ToParameterDescriptor()
                {
                    return new ParameterDescriptor();
                }
            }

            public class RetryTestListener : IListener
            {
                private ListenerFactoryContext _context;
                RetryTestExtension _parent;
                CancellationTokenSource _cts = new CancellationTokenSource();

                public RetryTestListener(ListenerFactoryContext context, RetryTestExtension parent)
                {
                    _context = context;
                    _parent = parent;
                }

                void IListener.Cancel()
                {
                    // nop
                }

                void IDisposable.Dispose()
                {
                    // nop
                }

                Task IListener.StartAsync(CancellationToken cancellationToken)
                {
                    _parent.OnEvent += new EventHandler(async (s, e) =>
                    {
                        TriggeredFunctionData data = new TriggeredFunctionData()
                        {
                            TriggerValue = "test"
                        };

                        FunctionResult result = await _context.Executor.TryExecuteAsync(data, CancellationToken.None);
                    });
                    return Task.FromResult(0);
                }

                Task IListener.StopAsync(CancellationToken cancellationToken)
                {
                    _cts.Cancel();
                    return Task.FromResult(0);
                }
            }

            class RetryTestValueProvider : IValueProvider
            {
                protected RetryTestExtension _parent;

                public RetryTestValueProvider(RetryTestExtension parent)
                {
                    _parent = parent;
                }

                public Type Type => typeof(string);

                public object Value;

                public Task<object> GetValueAsync()
                {
                    return Task.FromResult(Value);
                }
                public string ToInvokeString()
                {
                    return Value.ToString();
                }
            }

            class RetryTestReturnValueProvider : RetryTestValueProvider, IValueBinder
            {
                public RetryTestReturnValueProvider(RetryTestExtension parent) : base(parent)
                {
                }

                public Task SetValueAsync(object value, CancellationToken cancellationToken)
                {
                    return Task.CompletedTask;
                }
            }
        }
    }
}
