﻿// Copyright (c) .NET Foundation. All rights reserved.
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

namespace Microsoft.Azure.WebJobs.Host.Listeners
{
    internal class HostListenerFactory : IListenerFactory
    {
        private static readonly MethodInfo JobActivatorCreateMethod = typeof(IJobActivator).GetMethod("CreateInstance", BindingFlags.Public | BindingFlags.Instance).GetGenericMethodDefinition();
        private const string IsDisabledFunctionName = "IsDisabled";
        private readonly IEnumerable<IFunctionDefinition> _functionDefinitions;
        private readonly SingletonManager _singletonManager;
        private readonly IJobActivator _activator;
        private readonly INameResolver _nameResolver;
        private readonly TraceWriter _trace;

        public HostListenerFactory(IEnumerable<IFunctionDefinition> functionDefinitions, SingletonManager singletonManager, IJobActivator activator, INameResolver nameResolver, TraceWriter trace)
        {
            _functionDefinitions = functionDefinitions;
            _singletonManager = singletonManager;
            _activator = activator;
            _nameResolver = nameResolver;
            _trace = trace;
        }

        public async Task<IListener> CreateAsync(CancellationToken cancellationToken)
        {
            List<IListener> listeners = new List<IListener>();

            foreach (IFunctionDefinition functionDefinition in _functionDefinitions)
            {
                IListenerFactory listenerFactory = functionDefinition.ListenerFactory;
                if (listenerFactory == null)
                {
                    continue;
                }

                // Determine if the function is disabled
                MethodInfo method = functionDefinition.Descriptor.Method;
                if (IsDisabled(method, _nameResolver, _activator))
                {
                    _trace.Info(string.Format("Function '{0}' is disabled", functionDefinition.Descriptor.ShortName), TraceSource.Host);
                    continue;
                }

                IListener listener = await listenerFactory.CreateAsync(cancellationToken);

                // if the listener is a Singleton, wrap it with our SingletonListener
                SingletonAttribute singletonAttribute = SingletonManager.GetListenerSingletonOrNull(listener.GetType(), method);
                if (singletonAttribute != null)
                {
                    listener = new SingletonListener(method, singletonAttribute, _singletonManager, listener, _trace);
                }

                // wrap the listener with a function listener to handle exceptions
                listener = new FunctionListener(listener, functionDefinition.Descriptor, _trace);
                listeners.Add(listener);
            }

            return new CompositeListener(listeners);
        }

        internal static bool IsDisabled(MethodInfo method, INameResolver nameResolver, IJobActivator activator)
        {
            ParameterInfo triggerParameter = method.GetParameters().FirstOrDefault();
            if (triggerParameter != null)
            {
                // look for the first DisableAttribute up the hierarchy
                DisableAttribute disableAttribute = TypeUtility.GetHierarchicalAttributeOrNull<DisableAttribute>(triggerParameter);
                if (disableAttribute != null)
                {
                    if (!string.IsNullOrEmpty(disableAttribute.SettingName))
                    {
                        return IsDisabledBySetting(disableAttribute.SettingName, method, nameResolver);
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

        internal static bool IsDisabledBySetting(string settingName, MethodInfo method, INameResolver nameResolver)
        {
            if (nameResolver != null)
            {
                settingName = nameResolver.ResolveWholeString(settingName);
            }

            BindingTemplate bindingTemplate = BindingTemplate.FromString(settingName);
            Dictionary<string, string> bindingData = new Dictionary<string, string>();
            bindingData.Add("MethodName", string.Format(CultureInfo.InvariantCulture, "{0}.{1}", method.DeclaringType.Name, method.Name));
            bindingData.Add("MethodShortName", method.Name);
            settingName = bindingTemplate.Bind(bindingData);

            // check the target setting and return false (disabled) if the value exists
            // and is "falsey"
            string value = ConfigurationUtility.GetSettingFromConfigOrEnvironment(settingName);
            if (!string.IsNullOrEmpty(value) &&
                (string.Compare(value, "1", StringComparison.OrdinalIgnoreCase) == 0 ||
                 string.Compare(value, "true", StringComparison.OrdinalIgnoreCase) == 0))
            {
                return true;
            }

            return false;
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
    }
}
