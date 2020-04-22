// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Executors;
using System.Security.Cryptography;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Collections.ObjectModel;

namespace Microsoft.Azure.WebJobs.Host.Triggers
{
    internal class TriggeredFunctionBinding<TTriggerValue> : ITriggeredFunctionBindingData<TTriggerValue>
    {
        private readonly FunctionDescriptor _descriptor;
        private readonly string _triggerParameterName;
        private readonly ITriggerBinding _triggerBinding;
        private readonly IReadOnlyDictionary<string, IBinding> _nonTriggerBindings;
        private readonly SingletonManager _singletonManager;

        public TriggeredFunctionBinding(FunctionDescriptor descriptor, string triggerParameterName, ITriggerBinding triggerBinding,
            IReadOnlyDictionary<string, IBinding> nonTriggerBindings, SingletonManager singletonManager)
        {
            _descriptor = descriptor;
            _triggerParameterName = triggerParameterName;
            _triggerBinding = triggerBinding;
            _nonTriggerBindings = nonTriggerBindings;
            _singletonManager = singletonManager;
        }

        public async Task<IReadOnlyDictionary<string, IValueProvider>> BindAsync(ValueBindingContext context, TTriggerValue value)
        {
            return await BindCoreAsync(context, value, null);
        }
        
        public async Task<IReadOnlyDictionary<string, InstrumentableObjectMetadata>> GetBindingDataAsync(ValueBindingContext context, TTriggerValue value)
        {
            return await GetBindingDataCoreAsync(context, value, null);
        }

        public async Task<IReadOnlyDictionary<string, IValueProvider>> BindAsync(ValueBindingContext context,
            IDictionary<string, object> parameters)
        {
            if (parameters == null || !parameters.ContainsKey(_triggerParameterName))
            {
                throw new InvalidOperationException("Missing value for trigger parameter '" + _triggerParameterName + "'.");
            }

            object value = parameters[_triggerParameterName];
            return await BindCoreAsync(context, value, parameters);
        }
        
        public async Task<IReadOnlyDictionary<string, InstrumentableObjectMetadata>> GetBindingDataAsync(ValueBindingContext context,
            IDictionary<string, object> parameters)
        {
            if (parameters == null || !parameters.ContainsKey(_triggerParameterName))
            {
                throw new InvalidOperationException("Missing value for trigger parameter '" + _triggerParameterName + "'.");
            }

            object value = parameters[_triggerParameterName];
            return await GetBindingDataCoreAsync(context, value, parameters);
        }

        private async Task<IReadOnlyDictionary<string, IValueProvider>> BindCoreAsync(ValueBindingContext context, object value, IDictionary<string, object> parameters)
        {
            Dictionary<string, IValueProvider> valueProviders = new Dictionary<string, IValueProvider>();
            IValueProvider triggerProvider = null;
            IReadOnlyDictionary<string, object> bindingData;
            bool isCacheTrigger = value.GetType() == typeof(CacheTriggeredInput);

            IValueBinder triggerReturnValueProvider= null;

            if (isCacheTrigger)
            {
                CacheTriggeredInput triggeredInput = (CacheTriggeredInput)value;
                string invokeString = triggeredInput.Metadata.ContainerName + "/" + triggeredInput.Metadata.Name;

                // If it is a byte array for out-of-proc languages, then we get the buffer from the cache and use that
                if (triggeredInput.Metadata.CacheObjectType == CacheObjectType.ByteArray)
                {
                    CacheServer cacheServer = CacheServer.Instance;
                    if (cacheServer.TryGetObjectByteArray(triggeredInput.Metadata, out byte[] buffer))
                    {
                        triggerProvider = new ConstantValueProvider(buffer, buffer.GetType(), invokeString);
                    }
                    else
                    {
                        Exception exception = new Exception("Unable to get byte array for cached object");
                        triggerProvider = new BindingExceptionValueProvider(_triggerParameterName, exception);
                    }
                }
                // Otherwise, we will later get the associated stream from the cache
                else
                {
                    triggerProvider = new ConstantValueProvider(value, value.GetType(), invokeString);
                }

                Dictionary<string, object> bindingDataDictionary = new Dictionary<string, object>
                {
                    { "name", triggeredInput.Metadata.Name }
                };
                bindingData = new ReadOnlyDictionary<string, object>(bindingDataDictionary);
            }
            else
            {
                try
                {
                    // TODO For objects that we already have in the cache, we can check here and not do the binding call
                    // Also we can instrument time taken to bind here - so for objects (like JavaScript byte[]) which we don't 
                    // wrap in the InstrumentableStream, we can get the time it took to bind them (which includes fetching from blob storage)
                    ITriggerData triggerData = await _triggerBinding.BindAsync(value, context);
                    triggerProvider = triggerData.ValueProvider;
                    bindingData = triggerData.BindingData;
                    triggerReturnValueProvider = (triggerData as TriggerData)?.ReturnValueProvider;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    triggerProvider = new BindingExceptionValueProvider(_triggerParameterName, exception);
                    bindingData = null;
                }
            }
            
            valueProviders.Add(_triggerParameterName, triggerProvider);

            // Bind Singleton if specified
            SingletonAttribute singletonAttribute = SingletonManager.GetFunctionSingletonOrNull(_descriptor, isTriggered: true);
            if (singletonAttribute != null)
            {
                string boundScopeId = _singletonManager.GetBoundScopeId(singletonAttribute.ScopeId, bindingData);
                IValueProvider singletonValueProvider = new SingletonValueProvider(_descriptor, boundScopeId, context.FunctionInstanceId.ToString(), singletonAttribute, _singletonManager);
                valueProviders.Add(SingletonValueProvider.SingletonParameterName, singletonValueProvider);
            }

            BindingContext bindingContext = FunctionBinding.NewBindingContext(context, bindingData, parameters);
            foreach (KeyValuePair<string, IBinding> item in _nonTriggerBindings)
            {
                string name = item.Key;
                IBinding binding = item.Value;
                IValueProvider valueProvider;

                try
                {
                    if (parameters != null && parameters.ContainsKey(name))
                    {
                        valueProvider = await binding.BindAsync(parameters[name], context);
                    }
                    else
                    {
                        valueProvider = await binding.BindAsync(bindingContext);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    valueProvider = new BindingExceptionValueProvider(name, exception);
                }

                valueProviders.Add(name, valueProvider);
            }

            // Triggers can optionally process the return values of functions. They do so by declaring
            // a "$return" key in their binding data dictionary and mapping it to an IValueBinder.
            // An explicit return binding takes precedence over an implicit trigger binding. 
            if (!valueProviders.ContainsKey(FunctionIndexer.ReturnParamName))
            {
                if (triggerReturnValueProvider != null)
                {
                    valueProviders.Add(FunctionIndexer.ReturnParamName, triggerReturnValueProvider);
                }
            }

            return valueProviders;
        }      
        
        private async Task<IReadOnlyDictionary<string, InstrumentableObjectMetadata>> GetBindingDataCoreAsync(ValueBindingContext context, object value, IDictionary<string, object> parameters)
        {
            Dictionary<string, InstrumentableObjectMetadata> valueProviders = new Dictionary<string, InstrumentableObjectMetadata>();
            IReadOnlyDictionary<string, object> bindingData;
            
            bool isCacheTriggered = value.GetType() == typeof(CacheTriggeredInput);
            InstrumentableObjectMetadata triggerMetadata = new InstrumentableObjectMetadata(isTriggerParameter: true, isCacheTriggered: isCacheTriggered);

            if (isCacheTriggered)
            {
                CacheTriggeredInput cacheTriggeredInput = (CacheTriggeredInput)value;
                triggerMetadata.Add("Uri", cacheTriggeredInput.Metadata.Uri);
                triggerMetadata.Add("Name", cacheTriggeredInput.Metadata.Name);
                triggerMetadata.Add("ContainerName", cacheTriggeredInput.Metadata.ContainerName);
                triggerMetadata.Add("ETag", cacheTriggeredInput.Metadata.Etag);
            }
            else
            {
                try
                {
                    ITriggerData triggerData = await _triggerBinding.BindAsync(value, context);
                    bindingData = triggerData.BindingData;
                    
                    if (bindingData.TryGetValue("Uri", out object rawUri))
                    {
                        Uri uri = (Uri)rawUri;
                        triggerMetadata.Add("Uri", uri.ToString());
                        string containerName = uri.Segments[1];
                        if (containerName.EndsWith("/"))
                        {
                            containerName = containerName.Remove(containerName.Length - 1);
                        }
                        triggerMetadata.Add("ContainerName", containerName);
                    }
                    if (bindingData.TryGetValue("Name", out object name))
                    {
                        triggerMetadata.Add("Name", ((string)name));
                    }
                    if (bindingData.TryGetValue("Properties", out object properties))
                    {
                        try
                        {
                            string contentMd5 = properties.GetType().GetProperty("ContentMD5")?.GetValue(properties, null)?.ToString();
                            triggerMetadata.Add("ContentMD5", contentMd5);
                            string length = properties.GetType().GetProperty("Length")?.GetValue(properties, null)?.ToString();
                            triggerMetadata.Add("Length", length.ToString());
                            string created = properties.GetType().GetProperty("Created")?.GetValue(properties, null)?.ToString();
                            triggerMetadata.Add("Created", created.ToString());
                            string lastModifed = properties.GetType().GetProperty("LastModified")?.GetValue(properties, null)?.ToString();
                            triggerMetadata.Add("LastModified", lastModifed.ToString());
                            string etag = properties.GetType().GetProperty("ETag")?.GetValue(properties, null)?.ToString();
                            triggerMetadata.Add("ETag", etag);
                        }
                        catch (Exception exception)
                        {
                            triggerMetadata.Add("Exception", exception.Message);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    bindingData = null;
                }
            }

            valueProviders.Add(_triggerParameterName, triggerMetadata);

            foreach (KeyValuePair<string, IBinding> item in _nonTriggerBindings)
            {
                string name = item.Key;
                IBinding binding = item.Value;
                InstrumentableObjectMetadata nonTriggerMetadata = new InstrumentableObjectMetadata(isTriggerParameter: false, isCacheTriggered: false);

                try
                {
                    ParameterDescriptor param = binding.ToParameterDescriptor();
                    if (param.GetType().ToString() == "Microsoft.Azure.WebJobs.Host.Protocols.BlobParameterDescriptor")
                    {
                        string access = param.GetType().GetProperty("Access")?.GetValue(param)?.ToString();
                        nonTriggerMetadata.Add("Access", access);
                        string accountName = param.GetType().GetProperty("AccountName")?.GetValue(param)?.ToString();
                        nonTriggerMetadata.Add("AccountName", accountName);
                        string blobName = param.GetType().GetProperty("BlobName")?.GetValue(param)?.ToString();
                        nonTriggerMetadata.Add("Name", blobName);
                        string containerName = param.GetType().GetProperty("ContainerName")?.GetValue(param)?.ToString();
                        nonTriggerMetadata.Add("ContainerName", containerName);
                    }
                }
                catch (Exception exception)
                {
                    nonTriggerMetadata.Add("Exception", exception.Message);
                }

                valueProviders.Add(name, nonTriggerMetadata);
            }

            // TODO singleton and return value details are not yet captured
            // Need to see when exactly they are used and if we need to instrument those too

            return valueProviders;
        }      
    }
}
