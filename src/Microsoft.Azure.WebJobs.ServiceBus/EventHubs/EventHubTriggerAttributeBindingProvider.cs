// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Listeners;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // $$$ this needs love
    internal class EventHubTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private INameResolver _nameResolver;
        private readonly IEventHubProvider _eventHubConfig;

        public EventHubTriggerAttributeBindingProvider(INameResolver nameResolver, IEventHubProvider eventHubConfig)
        {
            this._nameResolver = nameResolver;
            this._eventHubConfig = eventHubConfig;
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            EventHubTriggerAttribute attribute = parameter.GetCustomAttribute<EventHubTriggerAttribute>(inherit: false);

            if (attribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            string eventHubName = attribute.EventHubName;
            string resolvedName = _nameResolver.ResolveWholeString(eventHubName);
            var eventHostListener = _eventHubConfig.GetListener(resolvedName);
                        
            bool singleDispatch;
            
            ITriggerDataArgumentBinding<EventHubTriggerInput> argumentBinding = null;
            if (parameter.ParameterType.IsArray)
            {
                // dispatch the entire batch in a single call. 
                singleDispatch = false;

                var elementType = parameter.ParameterType.GetElementType();

                var innerArgumentBinding = GetArgumentBinding(elementType);

                argumentBinding = new ArrayArgumentBinding(innerArgumentBinding);
            }
            else
            {
                // Dispatch each item one at a time
                singleDispatch = true;

                var elementType = parameter.ParameterType;
                argumentBinding = GetArgumentBinding(elementType);
            }

            if (argumentBinding == null)
            {
                string msg = string.Format("Unsupported binder type: {0}. Bind to string[] or EventData[]", parameter.ParameterType);
                throw new InvalidOperationException(msg);
            }

            var options = _eventHubConfig.GetOptions();
            Func<ListenerFactoryContext, Task<IListener>> createListener =
                (factoryContext) =>
                {
                    IListener listener = new EventHubListener(factoryContext.Executor, eventHostListener, options, singleDispatch);
                    return Task.FromResult(listener);
                };

            ITriggerBinding binding = new CommonTriggerBinding<EventHubTriggerInput>(argumentBinding, createListener); 

            return Task.FromResult<ITriggerBinding>(binding);         
        }


        
        // --> EventData
        // --> string
        // --> Poco 
        private SimpleArgumentBinding GetArgumentBinding(Type elementType)
        {
            if (elementType == typeof(EventData))
            {
                return new SimpleArgumentBinding();
            }
            if (elementType == typeof(string))
            {
                return new StringArgumentBinding();
            }
            else
            {
                return new PocoArgumentBinding(elementType);
            }

            string msg = string.Format("Unsupported binder type: {0}. Bind to string[] or EventData[]", elementType);
            throw new InvalidOperationException(msg);
        }

        // Array wrapper - can compose with any other elemental binding. 
        // Bind to an Array<T>
        class ArrayArgumentBinding : SimpleArgumentBinding
        {
            private readonly SimpleArgumentBinding _innerBinding;

            // $$$ Build this on direct interface?
            public ArrayArgumentBinding(SimpleArgumentBinding innerBinding)
            {
                this._innerBinding = innerBinding;
            }

            public override Task<ITriggerData> BindAsync(EventHubTriggerInput value, ValueBindingContext context)
            {
                Dictionary<string, object> bindingData = GetCommonBindingData(value);
                
                int len = value._events.Length;
                var elementType = _innerBinding._elementType;

                // $$$ this could be factored out to an IArray interface on EventHubBatch
                var array = Array.CreateInstance(elementType, len);
                for (int i = 0; i < len; i++)
                {
                    var eventData = value._events[i];
                    var obj = _innerBinding.Convert(eventData, null);
                    array.SetValue(obj, i);
                }
                var arrayType = elementType.MakeArrayType();

                IValueProvider valueProvider = new ConstantValueProvider
                {
                    _value = array,
                    Type = arrayType,
                };
                var triggerData = new TriggerData(valueProvider, bindingData);
                return Task.FromResult<ITriggerData>(triggerData);
            }
        }

        // $$$ Can we merge with C:\dev\AFunc\azure-webjobs-sdk\src\Microsoft.Azure.WebJobs.ServiceBus\Triggers\UserTypeArgumentBindingProvider.cs ?
        // This needs to populate the binding contract with the properties of the object. 
        class PocoArgumentBinding : StringArgumentBinding
        {
            IBindingDataProvider _provider;

            public PocoArgumentBinding(Type elementType)
            {
                this._elementType = elementType;

                // Add properties ot binding data 
                _provider = BindingDataProvider.FromType(elementType);

                // Binding data from Poco properties takes precedence over builtins
                foreach (var kv in _provider.Contract)
                {
                    string name = kv.Key;
                    Type type = kv.Value;
                    _contract[name] = type;
                }
            }

            internal override object Convert(EventData value, Dictionary<string, object> bindingData)
            {
                string json = ConvertEventData2String(value);
                var obj =JsonConvert.DeserializeObject(json, this._elementType);

                if (bindingData != null)
                {
                    var pocoData = _provider.GetBindingData(obj);

                    foreach (var kv in pocoData)
                    {
                        string propName = kv.Key;
                        object propVal = kv.Value;
                        bindingData[propName] = propVal;
                    }
                }

                return obj;
            }
        }

        // $$$ Use Conversion Manager
        // Bind EventData --> String
        class StringArgumentBinding : SimpleArgumentBinding
        {
            public StringArgumentBinding()
            {
                this._elementType = typeof(string);
            }

            protected static string ConvertEventData2String(EventData x)
            {
                return Encoding.UTF8.GetString(x.GetBytes());
            }

            internal override object Convert(EventData value, Dictionary<string, object> bindingData)
            {
                var obj = ConvertEventData2String(value);
                return obj;
            }
        }

        // Bind EventData to iteslef 
        class SimpleArgumentBinding : ITriggerDataArgumentBinding<EventHubTriggerInput>
        {
            const string DataContract_PartitionContext = "partitionContext";

            protected internal Type _elementType = typeof(EventData);
            protected Dictionary<string, Type> _contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            public SimpleArgumentBinding()
            {
                _contract[DataContract_PartitionContext] = typeof(PartitionContext);
            }

            public IReadOnlyDictionary<string, Type> BindingDataContract
            {
                get
                {
                    return _contract;
                }
            }

            public Type ValueType
            {
                get
                {
                    return typeof(EventHubTriggerInput);
                }
            }

            // Conver to target value, and add any binding data
            internal virtual object Convert(EventData value, Dictionary<string, object> bindingData)
            {
                return value;
            }

            public virtual Task<ITriggerData> BindAsync(EventHubTriggerInput value, ValueBindingContext context)
            {
                Dictionary<string, object> bindingData = GetCommonBindingData(value);

                var eventData = value._events[value._selector];
                var userValue = Convert(eventData, bindingData);
                IValueProvider valueProvider = new ConstantValueProvider
                {
                    _value = userValue,
                    Type = userValue.GetType() // same as _elementType
                };
                var triggerData = new TriggerData(valueProvider, bindingData);

                return Task.FromResult<ITriggerData>(triggerData);
            }

            protected static Dictionary<string, object> GetCommonBindingData(EventHubTriggerInput value)
            {
                Dictionary<string, object> bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                bindingData[DataContract_PartitionContext] = value._context;
                return bindingData;
            }        
        }
    } // end class
}