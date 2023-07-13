// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Listeners
{
    internal class HostListenerFactory : IListenerFactory
    {
        private static readonly MethodInfo JobActivatorCreateMethod = typeof(IJobActivator).GetMethod("CreateInstance", BindingFlags.Public | BindingFlags.Instance).GetGenericMethodDefinition();
        private const string IsDisabledFunctionName = "IsDisabled";
        private readonly IEnumerable<IFunctionDefinition> _functionDefinitions;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly Action _listenersCreatedCallback;
        private readonly IScaleMonitorManager _monitorManager;
        private readonly ITargetScalerManager _targetScalerManager;
        private readonly IDrainModeManager _drainModeManager;
        private readonly IEnumerable<IListenerDecorator> _listenerDecorators;

        public HostListenerFactory(IEnumerable<IFunctionDefinition> functionDefinitions, ILoggerFactory loggerFactory, IScaleMonitorManager monitorManager, ITargetScalerManager targetScalerManager, IEnumerable<IListenerDecorator> listenerDecorators, Action listenersCreatedCallback, IDrainModeManager drainModeManager = null)
        {
            _functionDefinitions = functionDefinitions;
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory?.CreateLogger(LogCategories.Startup);
            _monitorManager = monitorManager;
            _targetScalerManager = targetScalerManager;
            _listenersCreatedCallback = listenersCreatedCallback;
            _drainModeManager = drainModeManager;
            _listenerDecorators = listenerDecorators;
        }

        public async Task<IListener> CreateAsync(CancellationToken cancellationToken)
        {
            List<IListener> listeners = new List<IListener>();

            foreach (IFunctionDefinition functionDefinition in _functionDefinitions)
            {
                // Determine if the function is disabled
                if (functionDefinition.Descriptor.IsDisabled)
                {
                    _logger?.LogInformation($"Function '{functionDefinition.Descriptor.ShortName}' is disabled");
                    continue;
                }

                // Create the listener
                IListenerFactory listenerFactory = functionDefinition.ListenerFactory;
                if (listenerFactory == null)
                {
                    continue;
                }
                IListener listener = await listenerFactory.CreateAsync(cancellationToken);

                RegisterScalers(listener);

                listener = ApplyDecorators(listener, functionDefinition);

                listeners.Add(listener);
            }

            _listenersCreatedCallback?.Invoke();

            var compositeListener = new CompositeListener(listeners);
            _drainModeManager?.RegisterListener(compositeListener);

            return compositeListener;
        }

        internal void RegisterScalers(IListener listener)
        {
            RegisterScaleMonitor(listener, _monitorManager);
            RegisterTargetScaler(listener, _targetScalerManager);
        }

        /// <summary>
        /// Check to see if the specified listener is an <see cref="IScaleMonitor"/> and if so
        /// register it with the <see cref="IScaleMonitorManager"/>.
        /// </summary>
        /// <remarks>
        /// Note that disabled functions won't have their monitors registered. Therefore we'll only be
        /// monitoring valid, non-disabled functions which is what we want.
        /// Similarly, any functions failing indexing won't have their monitors registered.
        /// </remarks>
        /// <param name="listener">The listener to check and register a monitor for.</param>
        /// <param name="monitorManager">The monitor manager to register to.</param>
        internal static void RegisterScaleMonitor(IListener listener, IScaleMonitorManager monitorManager)
        {
            if (listener is IScaleMonitor scaleMonitor)
            {
                monitorManager.Register(scaleMonitor);
            }
            else if (listener is IScaleMonitorProvider)
            {
                var monitor = ((IScaleMonitorProvider)listener).GetMonitor();
                monitorManager.Register(monitor);
            }
            else if (listener is IEnumerable<IListener>)
            {
                // for composite listeners, we need to check all the inner listeners
                foreach (var innerListener in ((IEnumerable<IListener>)listener))
                {
                    RegisterScaleMonitor(innerListener, monitorManager);
                }
            }
        }

        internal static void RegisterTargetScaler(IListener listener, ITargetScalerManager targetScalerManager)
        {
            if (listener is ITargetScaler targetScaler)
            {
                targetScalerManager.Register(targetScaler);
            }
            else if (listener is ITargetScalerProvider targetScalerProvider)
            {
                var scaler = ((ITargetScalerProvider)listener).GetTargetScaler();
                targetScalerManager.Register(scaler);
            }
            else if (listener is IEnumerable<IListener>)
            {
                // for composite listeners, we need to check all the inner listeners
                foreach (var innerListener in ((IEnumerable<IListener>)listener))
                {
                    RegisterTargetScaler(innerListener, targetScalerManager);
                }
            }
        }

        internal static bool IsDisabled(MethodInfo method, INameResolver nameResolver, IJobActivator activator, IConfiguration configuration)
        {
            // First try to resolve disabled state by setting
            string settingName = string.Format(CultureInfo.InvariantCulture, "AzureWebJobs.{0}.Disabled", Utility.GetFunctionName(method));
            // Linux does not support dots in env variable name. So we replace dots with underscores.
            string settingNameLinux = string.Format(CultureInfo.InvariantCulture, "AzureWebJobs_{0}_Disabled", Utility.GetFunctionName(method));
            if (configuration.IsSettingEnabled(settingName) || configuration.IsSettingEnabled(settingNameLinux))
            {
                return true;
            }
            else
            {
                // Second try to resolve disabled state by attribute
                ParameterInfo triggerParameter = method.GetParameters().FirstOrDefault();
                if (triggerParameter != null)
                {
                    // look for the first DisableAttribute up the hierarchy
                    DisableAttribute disableAttribute = TypeUtility.GetHierarchicalAttributeOrNull<DisableAttribute>(triggerParameter);
                    if (disableAttribute != null)
                    {
                        if (!string.IsNullOrEmpty(disableAttribute.SettingName))
                        {
                            return IsDisabledBySetting(disableAttribute.SettingName, method, nameResolver, configuration);
                        }
                        else if (disableAttribute.ProviderType != null)
                        {
                            // a custom provider Type has been specified
                            return IsDisabledByProvider(disableAttribute.ProviderType, method, activator);
                        }
                        else
                        {
                            // the default constructor was used
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        internal static bool IsDisabledBySetting(string settingName, MethodInfo method, INameResolver nameResolver, IConfiguration configuration)
        {
            if (nameResolver != null)
            {
                settingName = nameResolver.ResolveWholeString(settingName);
            }

            BindingTemplate bindingTemplate = BindingTemplate.FromString(settingName);
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("MethodName", string.Format(CultureInfo.InvariantCulture, "{0}.{1}", method.DeclaringType.Name, method.Name));
            bindingData.Add("MethodShortName", method.Name);
            settingName = bindingTemplate.Bind(bindingData);

            return configuration.IsSettingEnabled(settingName);
        }

        internal static bool IsDisabledByProvider(Type providerType, MethodInfo jobFunction, IJobActivator activator)
        {
            MethodInfo methodInfo = providerType.GetMethod(IsDisabledFunctionName, BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(MethodInfo) }, null);
            if (methodInfo == null)
            {
                methodInfo = providerType.GetMethod(IsDisabledFunctionName, BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(MethodInfo) }, null);
            }

            if (methodInfo == null || methodInfo.ReturnType != typeof(bool))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    "Type '{0}' must declare a method 'IsDisabled' returning bool and taking a single parameter of Type MethodInfo.", providerType.Name));
            }

            if (methodInfo.IsStatic)
            {
                return (bool)methodInfo.Invoke(null, new object[] { jobFunction });
            }
            else
            {
                MethodInfo createMethod = JobActivatorCreateMethod.MakeGenericMethod(providerType);
                object instance = createMethod.Invoke(activator, null);
                return (bool)methodInfo.Invoke(instance, new object[] { jobFunction });
            }
        }

        /// <summary>
        /// Applies any user registered decorators, followed by any platform decorators.
        /// See <see cref="WebJobsServiceCollectionExtensions.AddListenerDecorators(Extensions.DependencyInjection.IServiceCollection)"/>.
        /// </summary>
        /// <param name="listener">The listener to apply decorators to.</param>
        /// <param name="functionDefinition">The function the the listener is for.</param>
        /// <returns>The resulting listener with decorators applied.</returns>
        private IListener ApplyDecorators(IListener listener, IFunctionDefinition functionDefinition)
        {
            Type rootListenerType = listener.GetType();
            var platformDecorators = _listenerDecorators.Where(p => p.GetType().Assembly == typeof(HostListenerFactory).Assembly);
            var userDecorators = _listenerDecorators.Except(platformDecorators);

            listener = ApplyDecorators(userDecorators, listener, functionDefinition, rootListenerType, writeLog: true);

            // Order is important - platform decorators must be applied AFTER any user decorators, in order.
            listener = ApplyDecorators(platformDecorators, listener, functionDefinition, rootListenerType);

            return listener;
        }

        private IListener ApplyDecorators(IEnumerable<IListenerDecorator> decorators, IListener listener, IFunctionDefinition functionDefinition, Type rootListenerType, bool writeLog = false)
        {
            foreach (var decorator in decorators)
            {
                var context = new ListenerDecoratorContext(functionDefinition, rootListenerType, listener);
                listener = decorator.Decorate(context);

                if (writeLog)
                {
                    _logger.LogDebug($"Applying IListenerDecorator '{decorator.GetType().FullName}'");
                }
            }

            return listener;
        }
    }
}
