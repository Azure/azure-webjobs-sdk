// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Listeners
{
    public class HostListenerFactoryTests
    {
        private readonly IJobActivator _jobActivator;
        private readonly IHost _testHost;
        private IConfiguration _configuration;

        public HostListenerFactoryTests()
        {
            DisableProvider_Static.Method = null;
            DisableProvider_Instance.Method = null;

            _testHost = CreateTestJobHost<Functions1>();
            _jobActivator = _testHost.Services.GetService<IJobActivator>();
            _configuration = _testHost.Services.GetService<IConfiguration>();
        }

        [Fact]
        public async Task CreateAsync_AppliesListenerDecorators()
        {
            HostListenerFactory factory = await CreateHostListenerFactoryAsync();
            IListener listener = await factory.CreateAsync(CancellationToken.None);

            // access the root inner listener
            var innerListeners = ((IEnumerable<IListener>)listener).ToArray();
            var innerListener = innerListeners[0];

            // expect the first listener to be our platform FunctionListener
            FunctionListener functionListener = (FunctionListener)innerListener;
            var innerListenerField = typeof(FunctionListener).GetField("_listener", BindingFlags.NonPublic | BindingFlags.Instance);
            TestListenerDecorator.DecoratorListener decoratorListener = (TestListenerDecorator.DecoratorListener)innerListenerField.GetValue(functionListener);

            // verify all decorators were consulted, resulting in a nested stack of listeners
            Assert.Equal("C", decoratorListener.Tag);
            decoratorListener = (TestListenerDecorator.DecoratorListener)decoratorListener.InnerListener;
            Assert.Equal("B", decoratorListener.Tag);
            decoratorListener = (TestListenerDecorator.DecoratorListener)decoratorListener.InnerListener;
            Assert.Equal("A", decoratorListener.Tag);

            // after decorators, we expect the Singleton listener
            SingletonListener singletonListener = (SingletonListener)decoratorListener.InnerListener;

            // finally, the QueueListener
            innerListenerField = typeof(SingletonListener).GetField("_innerListener", BindingFlags.NonPublic | BindingFlags.Instance);
            innerListener = (IListener)innerListenerField.GetValue(singletonListener);
            Assert.Equal("QueueListener", innerListener.GetType().Name);

            var logProvider = _testHost.GetTestLoggerProvider();
            var logs = logProvider.GetAllLogMessages().Where(p => p.Category == LogCategories.Startup && p.FormattedMessage.Contains(nameof(IListenerDecorator))).ToArray();
            Assert.Equal(3, logs.Length);
            Assert.Equal($"Applying IListenerDecorator '{typeof(TestListenerDecoratorA).FullName}'", logs[0].FormattedMessage);
            Assert.Equal($"Applying IListenerDecorator '{typeof(TestListenerDecoratorB).FullName}'", logs[1].FormattedMessage);
            Assert.Equal($"Applying IListenerDecorator '{typeof(TestListenerDecoratorC).FullName}'", logs[2].FormattedMessage);
        }

        [Fact]
        public async Task CreateAsync_RegistersScaleMonitors()
        {
            Mock<IFunctionDefinition> mockFunctionDefinition = new Mock<IFunctionDefinition>();
            Mock<IFunctionInstanceFactory> mockInstanceFactory = new Mock<IFunctionInstanceFactory>(MockBehavior.Strict);
            Mock<IListenerFactory> mockListenerFactory = new Mock<IListenerFactory>(MockBehavior.Strict);
            var testListener = new TestListener_Monitor();
            mockListenerFactory.Setup(p => p.CreateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(testListener);
            SingletonManager singletonManager = new SingletonManager();

            ILoggerFactory loggerFactory = new LoggerFactory();
            TestLoggerProvider loggerProvider = new TestLoggerProvider();
            loggerFactory.AddProvider(loggerProvider);

            List<FunctionDefinition> functions = new List<FunctionDefinition>();
            var method = typeof(Functions1).GetMethod("TestJob", BindingFlags.Public | BindingFlags.Static);
            FunctionDescriptor descriptor = FunctionIndexer.FromMethod(method, _configuration, _jobActivator);
            FunctionDefinition definition = new FunctionDefinition(descriptor, mockInstanceFactory.Object, mockListenerFactory.Object);
            functions.Add(definition);

            var monitorManager = new ScaleMonitorManager();
            var targetScaleManager = new TargetScalerManager();
            var drainModeManagerMock = new Mock<IDrainModeManager>();
            var listenerDecorators = _testHost.Services.GetServices<IListenerDecorator>();
            HostListenerFactory factory = new HostListenerFactory(functions, loggerFactory, monitorManager, targetScaleManager, listenerDecorators, () => { }, drainModeManagerMock.Object);
            IListener listener = await factory.CreateAsync(CancellationToken.None);

            var innerListeners = ((IEnumerable<IListener>)listener).ToArray();

            var monitors = monitorManager.GetMonitors().ToArray();
            Assert.Single(monitors);
            Assert.Same(testListener, monitors[0]);
        }

        [Fact]
        public async Task CreateAsync_RegistersTargetScalers()
        {
            Mock<IFunctionDefinition> mockFunctionDefinition = new Mock<IFunctionDefinition>();
            Mock<IFunctionInstanceFactory> mockInstanceFactory = new Mock<IFunctionInstanceFactory>(MockBehavior.Strict);
            Mock<IListenerFactory> mockListenerFactory = new Mock<IListenerFactory>(MockBehavior.Strict);
            var testListener = new TestListener_TargetScaler();
            mockListenerFactory.Setup(p => p.CreateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(testListener);
            SingletonManager singletonManager = new SingletonManager();

            ILoggerFactory loggerFactory = new LoggerFactory();
            TestLoggerProvider loggerProvider = new TestLoggerProvider();
            loggerFactory.AddProvider(loggerProvider);

            List<FunctionDefinition> functions = new List<FunctionDefinition>();
            var method = typeof(Functions1).GetMethod("TestJob", BindingFlags.Public | BindingFlags.Static);
            FunctionDescriptor descriptor = FunctionIndexer.FromMethod(method, _configuration, _jobActivator);
            FunctionDefinition definition = new FunctionDefinition(descriptor, mockInstanceFactory.Object, mockListenerFactory.Object);
            functions.Add(definition);

            var monitorManager = new ScaleMonitorManager();
            var targetScaleManager = new TargetScalerManager();
            var drainModeManagerMock = new Mock<IDrainModeManager>();
            var listenerDecorators = _testHost.Services.GetServices<IListenerDecorator>();
            HostListenerFactory factory = new HostListenerFactory(functions, loggerFactory, monitorManager, targetScaleManager, listenerDecorators, () => { }, drainModeManagerMock.Object);
            IListener listener = await factory.CreateAsync(CancellationToken.None);

            var innerListeners = ((IEnumerable<IListener>)listener).ToArray();

            var targetScalers = targetScaleManager.GetTargetScalers().ToArray();
            Assert.Single(targetScalers);
            Assert.Same(testListener, targetScalers[0]);
        }

        [Fact]
        public void RegisterScaleMonitor_Succeeds()
        {
            // listener is a direct monitor
            var monitorManager = new ScaleMonitorManager();
            var testListener = new TestListener_Monitor();
            HostListenerFactory.RegisterScaleMonitor(testListener, monitorManager);
            var monitors = monitorManager.GetMonitors().ToArray();
            Assert.Single(monitors);
            Assert.Same(testListener, monitors[0]);

            // listener is a monitor provider
            monitorManager = new ScaleMonitorManager();
            var testListenerMonitorProvider = new TestListener_MonitorProvider();
            HostListenerFactory.RegisterScaleMonitor(testListenerMonitorProvider, monitorManager);
            monitors = monitorManager.GetMonitors().ToArray();
            Assert.Single(monitors);
            Assert.Same(testListenerMonitorProvider.GetMonitor(), monitors[0]);

            // listener is composite, so we expect recursion
            monitorManager = new ScaleMonitorManager();
            var compositListener = new CompositeListener(testListener, testListenerMonitorProvider);
            HostListenerFactory.RegisterScaleMonitor(compositListener, monitorManager);
            monitors = monitorManager.GetMonitors().ToArray();
            Assert.Equal(2, monitors.Length);
            Assert.Same(testListener, monitors[0]);
            Assert.Same(testListenerMonitorProvider.GetMonitor(), monitors[1]);
        }

        [Fact]
        public void RegisterTargetScaler_Succeeds()
        {
            // listener is a direct target scaler
            var manager = new TargetScalerManager();
            var testListener = new TestListener_TargetScaler();
            HostListenerFactory.RegisterTargetScaler(testListener, manager);
            var targetScalers = manager.GetTargetScalers().ToArray();
            Assert.Single(targetScalers);
            Assert.Same(testListener, targetScalers[0]);

            // listener is a target scaler provider
            manager = new TargetScalerManager();
            var providerListener = new TestListener_TargetScalerProvider();
            HostListenerFactory.RegisterTargetScaler(providerListener, manager);
            targetScalers = manager.GetTargetScalers().ToArray();
            Assert.Single(targetScalers);
            Assert.Same(providerListener.GetTargetScaler(), targetScalers[0]);

            // listener is composite, so we expect recursion
            manager = new TargetScalerManager();
            var compositListener = new CompositeListener(testListener, providerListener);
            HostListenerFactory.RegisterTargetScaler(compositListener, manager);
            targetScalers = manager.GetTargetScalers().ToArray();
            Assert.Equal(2, targetScalers.Length);
            Assert.Same(testListener, targetScalers[0]);
            Assert.Same(providerListener.GetTargetScaler(), targetScalers[1]);
        }

        [Theory]
        [InlineData(typeof(Functions1), "DisabledAtParameterLevel")]
        [InlineData(typeof(Functions1), "DisabledAtMethodLevel")]
        [InlineData(typeof(Functions1), "DisabledAtMethodLevel_Boolean")]
        [InlineData(typeof(Functions1), "DisabledAtMethodLevel_CustomType_Static")]
        [InlineData(typeof(Functions1), "DisabledAtMethodLevel_CustomType_Instance")]
        [InlineData(typeof(Functions1), "DisabledByEnvironmentSetting")]
        [InlineData(typeof(Functions1), "DisabledByAppSetting_Method")]
        [InlineData(typeof(Functions1), "DisabledByAppSetting_FunctionNameAttributeTest")]
        [InlineData(typeof(Functions1), "DisabledByAppSetting_Method_Linux")]
        [InlineData(typeof(Functions1), "DisabledByAppSetting_FunctionNameAttributeTest_Linux")]
        [InlineData(typeof(Functions2), "DisabledAtClassLevel")]
        public async Task CreateAsync_SkipsDisabledFunctions(Type jobType, string methodName)
        {
            Environment.SetEnvironmentVariable("EnvironmentSettingTrue", "True");

            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            Mock<IFunctionDefinition> mockFunctionDefinition = new Mock<IFunctionDefinition>();
            Mock<IFunctionInstanceFactory> mockInstanceFactory = new Mock<IFunctionInstanceFactory>(MockBehavior.Strict);
            Mock<IListenerFactory> mockListenerFactory = new Mock<IListenerFactory>(MockBehavior.Strict);
            SingletonManager singletonManager = new SingletonManager();

            ILoggerFactory loggerFactory = new LoggerFactory();
            TestLoggerProvider loggerProvider = new TestLoggerProvider();
            loggerFactory.AddProvider(loggerProvider);

            // create a bunch of function definitions that are disabled
            List<FunctionDefinition> functions = new List<FunctionDefinition>();
            var method = jobType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            FunctionDescriptor descriptor = FunctionIndexer.FromMethod(method, _configuration, _jobActivator);
            FunctionDefinition definition = new FunctionDefinition(descriptor, mockInstanceFactory.Object, mockListenerFactory.Object);
            functions.Add(definition);

            // Create the composite listener - this will fail if any of the
            // function definitions indicate that they are not disabled
            var monitorManagerMock = new Mock<IScaleMonitorManager>(MockBehavior.Strict);
            var targetScalerManagerMock = new Mock<ITargetScalerManager>(MockBehavior.Strict);
            var drainModeManagerMock = new Mock<IDrainModeManager>();
            var listenerDecorators = _testHost.Services.GetServices<IListenerDecorator>();
            HostListenerFactory factory = new HostListenerFactory(functions, loggerFactory, monitorManagerMock.Object, targetScalerManagerMock.Object, listenerDecorators, () => { }, drainModeManagerMock.Object);

            IListener listener = await factory.CreateAsync(CancellationToken.None);

            string expectedMessage = $"Function '{descriptor.ShortName}' is disabled";

            // Validate Logger
            var logMessage = loggerProvider.CreatedLoggers.Single().GetLogMessages().Single();
            Assert.Equal(LogLevel.Information, logMessage.Level);
            Assert.Equal(Logging.LogCategories.Startup, logMessage.Category);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);

            Environment.SetEnvironmentVariable("EnvironmentSettingTrue", null);
        }

        [Fact]
        public void IsDisabledByProvider_ValidProvider_InvokesProvider()
        {
            Assert.Null(DisableProvider_Static.Method);
            MethodInfo method = typeof(Functions1).GetMethod("DisabledAtMethodLevel_CustomType_Static", BindingFlags.Public | BindingFlags.Static);
            HostListenerFactory.IsDisabledByProvider(typeof(DisableProvider_Static), method, _jobActivator);
            Assert.Same(method, DisableProvider_Static.Method);

            Assert.Null(DisableProvider_Instance.Method);
            method = typeof(Functions1).GetMethod("DisabledAtMethodLevel_CustomType_Static", BindingFlags.Public | BindingFlags.Static);
            HostListenerFactory.IsDisabledByProvider(typeof(DisableProvider_Instance), method, _jobActivator);
            Assert.Same(method, DisableProvider_Static.Method);
        }

        [Theory]
        [InlineData(typeof(InvalidProvider_MissingFunction))]
        [InlineData(typeof(InvalidProvider_InvalidSignature))]
        public void IsDisabledByProvider_InvalidProvider_Throws(Type providerType)
        {
            MethodInfo method = typeof(Functions1).GetMethod("DisabledAtMethodLevel_CustomType_Static", BindingFlags.Public | BindingFlags.Static);
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                HostListenerFactory.IsDisabledByProvider(providerType, method, _jobActivator);
            });
            Assert.Equal(string.Format("Type '{0}' must declare a method 'IsDisabled' returning bool and taking a single parameter of Type MethodInfo.", providerType.Name), ex.Message);
        }

        [Theory]
        [InlineData("Disable_{MethodName}_%Test%", true)]
        [InlineData("Disable_{MethodShortName}_%Test%", true)]
        [InlineData("Disable_TestJob", false)]
        [InlineData("Disable_TestJob_Blah", false)]
        public void IsDisabledBySetting_BindsSettingName(string settingName, bool disabled)
        {
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Disable_Functions1.TestJob_TestValue", "1" },
                    { "Disable_TestJob_TestValue", "1" },
                    { "Disable_TestJob", "False" },
                })
                .Build();

            Mock<INameResolver> mockNameResolver = new Mock<INameResolver>(MockBehavior.Strict);
            mockNameResolver.Setup(p => p.Resolve("Test")).Returns("TestValue");

            MethodInfo method = typeof(Functions1).GetMethod("TestJob", BindingFlags.Public | BindingFlags.Static);

            bool result = HostListenerFactory.IsDisabledBySetting(settingName, method, mockNameResolver.Object, _configuration);
            Assert.Equal(result, disabled);
        }

        private async Task<HostListenerFactory> CreateHostListenerFactoryAsync()
        {
            var indexProvider = _testHost.Services.GetService<IFunctionIndexProvider>();
            var index = await indexProvider.GetAsync(CancellationToken.None);
            List<IFunctionDefinition> functions = new List<IFunctionDefinition>();
            functions.Add(index.LookupByName("TestJob"));

            // create the listener
            ILoggerFactory loggerFactory = _testHost.Services.GetService<ILoggerFactory>();
            var monitorManager = _testHost.Services.GetService<IScaleMonitorManager>();
            var targetScaleManager = _testHost.Services.GetService<ITargetScalerManager>();
            var drainModeManager = _testHost.Services.GetService<IDrainModeManager>();
            var listenerDecorators = _testHost.Services.GetServices<IListenerDecorator>();
            HostListenerFactory factory = new HostListenerFactory(functions, loggerFactory, monitorManager, targetScaleManager, listenerDecorators, () => { }, drainModeManager);

            return factory;
        }

        private IHost CreateTestJobHost<TProg>(Action<IHostBuilder> extraConfig = null)
        {
            var hostBuilder = new HostBuilder()
                .ConfigureDefaultTestHost<TProg>(b =>
                {
                    // For Queue and Blob Triggers
                    b.AddAzureStorageBlobs();
                    b.AddAzureStorageQueues();

                    b.AddAzureStorageCoreServices();
                })
                .ConfigureLogging((context, b) =>
                {
                    b.SetMinimumLevel(LogLevel.Debug);
                })
                .ConfigureServices(services =>
                {
                    // add some custom decorators that we expect to be applied in order, before any platform
                    // decorators
                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IListenerDecorator, TestListenerDecoratorA>());
                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IListenerDecorator, TestListenerDecoratorB>());
                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IListenerDecorator, TestListenerDecoratorC>());
                });

            extraConfig?.Invoke(hostBuilder); // test hook gets final say to replace. 

            IHost host = hostBuilder.Build();

            return host;
        }

        private class TestJobActivator : IJobActivator
        {
            private int _hostId;

            public TestJobActivator(int hostId)
            {
                _hostId = hostId;
            }

            public T CreateInstance<T>()
            {
                return (T)Activator.CreateInstance(typeof(T), _hostId);
            }
        }

        public class Functions1
        {
            public static void DisabledAtParameterLevel(
                [Disable("DisableSettingTrue")]
                [QueueTrigger("test")] string message)
            {
            }

            [Disable("DisableSetting1")]
            public static void DisabledAtMethodLevel(
                [QueueTrigger("test")] string message)
            {
            }

            [Disable]
            public static void DisabledAtMethodLevel_Boolean(
                [QueueTrigger("test")] string message)
            {
            }

            [Disable(typeof(DisableProvider_Static))]
            public static void DisabledAtMethodLevel_CustomType_Static(
                [QueueTrigger("test")] string message)
            {
            }

            [Disable(typeof(DisableProvider_Instance))]
            public static void DisabledAtMethodLevel_CustomType_Instance(
                [QueueTrigger("test")] string message)
            {
            }

            [Disable("EnvironmentSettingTrue")]
            public static void DisabledByEnvironmentSetting(
                [QueueTrigger("test")] string message)
            {
            }

            [Disable("Disable_{MethodName}_%Test%")]
            public static void DisabledByAppSetting_BindingData(
                [QueueTrigger("test")] string message)
            {
            }

            public static void DisabledByAppSetting_Method(
                [QueueTrigger("test")] string message)
            {
            }


            public static void DisabledByAppSetting_Method_Linux(
                [QueueTrigger("test")] string message)
            {
            }

            [FunctionName("DisabledByAppSetting_FunctionNameAttribute")]
            public static void DisabledByAppSetting_FunctionNameAttributeTest(
                [QueueTrigger("test")] string message)
            {
            }

            [FunctionName("DisabledByAppSetting_FunctionNameAttribute")]
            public static void DisabledByAppSetting_FunctionNameAttributeTest_Linux(
                [QueueTrigger("test")] string message)
            {
            }

            [Singleton(Mode = SingletonMode.Listener)]
            public static void TestJob(
                    [QueueTrigger("test")] string message)
            {
            }
        }

        [Disable("DisableSetting1")]
        public static class Functions2
        {
            public static void DisabledAtClassLevel(
                [QueueTrigger("test")] string message)
            {
            }
        }

        public class DisableProvider_Static
        {
            public static MethodInfo Method { get; set; }

            public static bool IsDisabled(MethodInfo method)
            {
                Method = method;
                return true;
            }
        }

        public class DisableProvider_Instance
        {
            public static MethodInfo Method { get; set; }

            public bool IsDisabled(MethodInfo method)
            {
                Method = method;
                return true;
            }
        }

        public class InvalidProvider_MissingFunction
        {
        }

        public class InvalidProvider_InvalidSignature
        {
            public static void IsDisabled(MethodInfo method)
            {
            }
        }

        public class TestListenerDecoratorA : TestListenerDecorator
        {
            public TestListenerDecoratorA() : base("A")
            {
            }
        }

        public class TestListenerDecoratorB : TestListenerDecorator
        {
            public TestListenerDecoratorB() : base("B")
            {
            }
        }

        public class TestListenerDecoratorC : TestListenerDecorator
        {
            public TestListenerDecoratorC() : base("C")
            {
            }
        }


        public abstract class TestListenerDecorator : IListenerDecorator
        {
            private string _tag;

            public TestListenerDecorator(string tag)
            {
                _tag = tag;
            }

            public IListener Decorate(ListenerDecoratorContext context)
            {
                return new DecoratorListener(context.Listener, _tag);
            }

            public class DecoratorListener : IListener
            {
                public DecoratorListener(IListener innerListener, string tag)
                {
                    InnerListener = innerListener;
                    Tag = tag;
                }

                internal string Tag { get; }

                internal IListener InnerListener { get; set; }

                public void Cancel()
                {
                }

                public void Dispose()
                {
                }

                public Task StartAsync(CancellationToken cancellationToken)
                {
                    return Task.CompletedTask;
                }

                public Task StopAsync(CancellationToken cancellationToken)
                {
                    return Task.CompletedTask;
                }
            }
        }

        public class TestListener : IListener
        {
            public void Cancel()
            {
            }

            public void Dispose()
            {
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        public class TestListener_Monitor : IListener, IScaleMonitor
        {
            public ScaleMonitorDescriptor Descriptor => throw new NotImplementedException();

            public void Cancel()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public Task<ScaleMetrics> GetMetricsAsync()
            {
                throw new NotImplementedException();
            }

            public ScaleStatus GetScaleStatus(ScaleStatusContext context)
            {
                throw new NotImplementedException();
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        public class TestListener_MonitorProvider : IListener, IScaleMonitorProvider
        {
            private readonly IScaleMonitor _monitor;

            public TestListener_MonitorProvider()
            {
                _monitor = new TestListener_MonitorImpl();
            }

            public void Cancel()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public IScaleMonitor GetMonitor()
            {
                return _monitor;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public class TestListener_MonitorImpl : IScaleMonitor
            {
                public ScaleMonitorDescriptor Descriptor => throw new NotImplementedException();

                public Task<ScaleMetrics> GetMetricsAsync()
                {
                    throw new NotImplementedException();
                }

                public ScaleStatus GetScaleStatus(ScaleStatusContext context)
                {
                    throw new NotImplementedException();
                }
            }
        }

        public class TestListener_TargetScaler : IListener, ITargetScaler
        {
            public TargetScalerDescriptor TargetScalerDescriptor => throw new NotImplementedException();

            public void Cancel()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context)
            {
                throw new NotImplementedException();
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        public class TestListener_TargetScalerProvider : IListener, ITargetScalerProvider
        {
            private readonly ITargetScaler _targetScaler;

            public TestListener_TargetScalerProvider()
            {
                _targetScaler = new TestListener_TargetScalerImpl();
            }

            public void Cancel()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public ITargetScaler GetTargetScaler()
            {
                return _targetScaler;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public class TestListener_TargetScalerImpl : ITargetScaler
            {
                public TargetScalerDescriptor TargetScalerDescriptor => throw new NotImplementedException();

                public Task<ScaleMetrics> GetMetricsAsync()
                {
                    throw new NotImplementedException();
                }

                public Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context)
                {
                    throw new NotImplementedException();
                }
            }
        }

        public class TestListener_MonitorAndTargetScaler : TestListener_Monitor, ITargetScaler
        {
            public TargetScalerDescriptor TargetScalerDescriptor => throw new NotImplementedException();

            public Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context)
            {
                throw new NotImplementedException();
            }
        }
    }
}
