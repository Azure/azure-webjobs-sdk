// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.ConsoleOutput;
using Microsoft.Azure.WebJobs.Host.Bindings.Invoke;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Host.Indexers
{
    internal class FunctionIndexer
    {
        private static readonly BindingFlags _publicMethodFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        private readonly ITriggerBindingProvider _triggerBindingProvider;
        private readonly IBindingProvider _bindingProvider;
        private readonly IJobActivator _activator;
        private readonly HashSet<Assembly> _jobTypeAssemblies;

        public FunctionIndexer(ITriggerBindingProvider triggerBindingProvider, IBindingProvider bindingProvider, IJobActivator activator, IExtensionRegistry extensions)
        {
            if (triggerBindingProvider == null)
            {
                throw new ArgumentNullException("triggerBindingProvider");
            }

            if (bindingProvider == null)
            {
                throw new ArgumentNullException("bindingProvider");
            }

            if (activator == null)
            {
                throw new ArgumentNullException("activator");
            }

            _triggerBindingProvider = triggerBindingProvider;
            _bindingProvider = bindingProvider;
            _activator = activator;
            _jobTypeAssemblies = new HashSet<Assembly>(GetJobTypeAssemblies(extensions, typeof(ITriggerBindingProvider), typeof(IBindingProvider)));
        }

        public async Task IndexTypeAsync(Type type, IFunctionIndexCollector index, CancellationToken cancellationToken)
        {
            foreach (MethodInfo method in type.GetMethods(_publicMethodFlags).Where(IsJobMethod))
            {
                await IndexMethodAsync(method, index, cancellationToken);
            }
        }

        public bool IsJobMethod(MethodInfo method)
        {
            if (method.ContainsGenericParameters)
            {
                return false;
            }

            if (method.GetCustomAttributesData().Any(HasJobAttribute))
            {
                return true;
            }

            if (method.GetParameters().Length == 0)
            {
                return false;
            }

            if (method.GetParameters().Any(p => p.GetCustomAttributesData().Any(HasJobAttribute)))
            {
                return true;
            }

            return false;
        }

        private static HashSet<Assembly> GetJobTypeAssemblies(IExtensionRegistry extensions, params Type[] extensionTypes)
        {
            // create a set containing our own core assemblies
            HashSet<Assembly> assemblies = new HashSet<Assembly>();
            assemblies.Add(typeof(BlobAttribute).Assembly);
       
            // add any extension assemblies
            foreach (Type extensionType in extensionTypes)
            {
                var currAssemblies = extensions.GetExtensions(extensionType).Select(p => p.GetType().Assembly);
                assemblies.UnionWith(currAssemblies);
            }

            return assemblies;
        }

        private bool HasJobAttribute(CustomAttributeData attributeData)
        {
            return _jobTypeAssemblies.Contains(attributeData.AttributeType.Assembly);
        }

        public async Task IndexMethodAsync(MethodInfo method, IFunctionIndexCollector index, CancellationToken cancellationToken)
        {
            try
            {
                await IndexMethodAsyncCore(method, index, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new FunctionIndexingException(method.Name, exception);
            }
        }

        internal async Task IndexMethodAsyncCore(MethodInfo method, IFunctionIndexCollector index,
            CancellationToken cancellationToken)
        {
            Debug.Assert(method != null);
            bool hasNoAutomaticTrigger = method.GetCustomAttribute<NoAutomaticTriggerAttribute>() != null;

            ITriggerBinding triggerBinding = null;
            ParameterInfo triggerParameter = null;
            ParameterInfo[] parameters = method.GetParameters();

            foreach (ParameterInfo parameter in parameters)
            {
                ITriggerBinding possibleTriggerBinding = await _triggerBindingProvider.TryCreateAsync(
                    new TriggerBindingProviderContext(parameter, cancellationToken));

                if (possibleTriggerBinding != null)
                {
                    if (triggerBinding == null)
                    {
                        triggerBinding = possibleTriggerBinding;
                        triggerParameter = parameter;
                    }
                    else
                    {
                        throw new InvalidOperationException("More than one trigger per function is not allowed.");
                    }
                }
            }

            Dictionary<string, IBinding> nonTriggerBindings = new Dictionary<string, IBinding>();
            IReadOnlyDictionary<string, Type> bindingDataContract;

            if (triggerBinding != null)
            {
                bindingDataContract = triggerBinding.BindingDataContract;
            }
            else
            {
                bindingDataContract = null;
            }

            bool hasParameterBindingAttribute = false;
            Exception invalidInvokeBindingException = null;

            foreach (ParameterInfo parameter in parameters)
            {
                if (parameter == triggerParameter)
                {
                    continue;
                }

                IBinding binding = await _bindingProvider.TryCreateAsync(new BindingProviderContext(parameter,
                    bindingDataContract, cancellationToken));

                if (binding == null)
                {
                    if (triggerBinding != null && !hasNoAutomaticTrigger)
                    {
                        throw new InvalidOperationException("Cannot bind parameter '" + parameter.Name +
                            "' when using this trigger.");
                    }
                    else
                    {
                        // Host.Call-only parameter
                        string parameterName = parameter.Name;
                        Type parameterType = parameter.ParameterType;

                        binding = InvokeBinding.Create(parameterName, parameterType);

                        if (binding == null && invalidInvokeBindingException == null)
                        {
                            // This function might not have any attribute, in which case we shouldn't throw an
                            // exception when we can't bind it. Instead, save this exception for later once we determine
                            // whether or not it is an SDK function.
                            invalidInvokeBindingException = new InvalidOperationException("Cannot bind parameter '" +
                                parameterName + "' to type " + parameterType.Name + ".");
                        }
                    }
                }
                else if (!hasParameterBindingAttribute)
                {
                    hasParameterBindingAttribute = binding.FromAttribute;
                }

                nonTriggerBindings.Add(parameter.Name, binding);
            }

            // Only index functions with some kind of attribute on them. Three ways that could happen:
            // 1. There's an attribute on a trigger parameter (all triggers come from attributes).
            // 2. There's an attribute on a non-trigger parameter (some non-trigger bindings come from attributes).
            if (triggerBinding == null && !hasParameterBindingAttribute)
            {
                // 3. There's an attribute on the method itself (NoAutomaticTrigger).
                if (method.GetCustomAttribute<NoAutomaticTriggerAttribute>() == null)
                {
                    return;
                }
            }

            Type returnType = method.ReturnType;

            if (returnType != typeof(void) && returnType != typeof(Task))
            {
                throw new InvalidOperationException("Functions must return Task or void.");
            }

            if (invalidInvokeBindingException != null)
            {
                throw invalidInvokeBindingException;
            }

            // Validation: prevent multiple ConsoleOutputs
            if (nonTriggerBindings.OfType<ConsoleOutputBinding>().Count() > 1)
            {
                throw new InvalidOperationException(
                    "Can't have multiple console output TextWriter parameters on a single function.");
            }

            string triggerParameterName = triggerParameter != null ? triggerParameter.Name : null;
            FunctionDescriptor functionDescriptor = CreateFunctionDescriptor(method, triggerParameterName, triggerBinding, nonTriggerBindings);
            IFunctionInvoker invoker = FunctionInvokerFactory.Create(method, _activator);
            IFunctionDefinition functionDefinition;

            if (triggerBinding != null)
            {
                functionDefinition = CreateFunctionDefinition(functionDescriptor, triggerBinding, triggerParameterName, nonTriggerBindings, invoker, functionDescriptor);

                if (hasNoAutomaticTrigger && functionDefinition != null)
                {
                    functionDefinition = new FunctionDefinition(functionDefinition.InstanceFactory, listenerFactory: null);
                }
            }
            else
            {
                IFunctionInstanceFactory instanceFactory = new FunctionInstanceFactory(
                    new FunctionBinding(method, nonTriggerBindings), invoker, functionDescriptor);
                functionDefinition = new FunctionDefinition(instanceFactory, listenerFactory: null);
            }

            index.Add(functionDefinition, functionDescriptor, method);
        }

        private static FunctionDefinition CreateFunctionDefinition(FunctionDescriptor descriptor, ITriggerBinding triggerBinding, string parameterName, IReadOnlyDictionary<string, IBinding> nonTriggerBindings, IFunctionInvoker invoker, FunctionDescriptor functionDescriptor)
        {
            Type triggerValueType = triggerBinding.GetType().GetInterface(typeof(ITriggerBinding<>).Name).GetGenericArguments()[0];

            // create the function binding
            Type genericType = typeof(TriggeredFunctionBinding<>).MakeGenericType(triggerValueType);
            IFunctionBinding functionBinding = (IFunctionBinding)Activator.CreateInstance(genericType, parameterName, triggerBinding, nonTriggerBindings);

            // create the instance factory
            genericType = typeof(TriggeredFunctionInstanceFactory<>).MakeGenericType(triggerValueType);
            IFunctionInstanceFactory instanceFactory = (IFunctionInstanceFactory)Activator.CreateInstance(genericType, functionBinding, invoker, functionDescriptor);

            genericType = typeof(TriggeredFunctionExecutorImpl<>).MakeGenericType(triggerValueType);
            ITriggeredFunctionExecutor triggerExecutor = (ITriggeredFunctionExecutor)Activator.CreateInstance(genericType, descriptor, instanceFactory);

            IListenerFactory listenerFactory = triggerBinding.CreateListenerFactory(triggerExecutor);

            return new FunctionDefinition(instanceFactory, listenerFactory);
        }

        private class TriggeredFunctionExecutorImpl<TTriggerValue> : ITriggeredFunctionExecutor
        {
            private FunctionDescription _description;
            private ITriggeredFunctionInstanceFactory<TTriggerValue> _instanceFactory;

            public TriggeredFunctionExecutorImpl(FunctionDescriptor descriptor, ITriggeredFunctionInstanceFactory<TTriggerValue> instanceFactory)
            {
                _description = new FunctionDescription
                {
                    ID = descriptor.Id,
                    FullName = descriptor.FullName
                };
                _instanceFactory = instanceFactory;
            }

            public FunctionDescription Function
            {
                get
                {
                    return _description;
                }
            }

            public async Task<bool> TryExecuteAsync(Guid? parentId, object triggerValue, ListenerExecutionContext context, CancellationToken cancellationToken)
            {
                IFunctionInstance instance = _instanceFactory.Create((TTriggerValue)triggerValue, parentId);
                IDelayedException exception = await context.FunctionExecutor.TryExecuteAsync(instance, cancellationToken);
                return exception == null;
            }
        }

        private static FunctionDescriptor CreateFunctionDescriptor(MethodInfo method, string triggerParameterName,
            ITriggerBinding triggerBinding, IReadOnlyDictionary<string, IBinding> nonTriggerBindings)
        {
            List<ParameterDescriptor> parameters = new List<ParameterDescriptor>();

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                string name = parameter.Name;

                if (name == triggerParameterName)
                {
                    parameters.Add(triggerBinding.ToParameterDescriptor());
                }
                else
                {
                    parameters.Add(nonTriggerBindings[name].ToParameterDescriptor());
                }
            }

            return new FunctionDescriptor
            {
                Id = method.GetFullName(),
                FullName = method.GetFullName(),
                ShortName = method.GetShortName(),
                Parameters = parameters
            };
        }
    }
}
