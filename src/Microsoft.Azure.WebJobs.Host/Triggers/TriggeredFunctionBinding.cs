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

        public async Task<IReadOnlyDictionary<string, IValueProvider>> BindAsync(ValueBindingContext context, TTriggerValue value, bool cacheTrigger)
        {
            return await BindCoreAsync(context, value, null, cacheTrigger);
        }
        
        public async Task<IReadOnlyDictionary<string, InstrumentableObjectMetadata>> GetBindingDataAsync(ValueBindingContext context, TTriggerValue value, bool cacheTrigger)
        {
            return await GetBindingDataCoreAsync(context, value, null, cacheTrigger);
        }

        public async Task<IReadOnlyDictionary<string, IValueProvider>> BindAsync(ValueBindingContext context,
            IDictionary<string, object> parameters, bool cacheTrigger)
        {
            if (parameters == null || !parameters.ContainsKey(_triggerParameterName))
            {
                throw new InvalidOperationException("Missing value for trigger parameter '" + _triggerParameterName + "'.");
            }

            object value = parameters[_triggerParameterName];
            return await BindCoreAsync(context, value, parameters, cacheTrigger);
        }
        
        public async Task<IReadOnlyDictionary<string, InstrumentableObjectMetadata>> GetBindingDataAsync(ValueBindingContext context,
            IDictionary<string, object> parameters, bool cacheTrigger)
        {
            if (parameters == null || !parameters.ContainsKey(_triggerParameterName))
            {
                throw new InvalidOperationException("Missing value for trigger parameter '" + _triggerParameterName + "'.");
            }

            object value = parameters[_triggerParameterName];
            return await GetBindingDataCoreAsync(context, value, parameters, cacheTrigger);
        }

        private async Task<IReadOnlyDictionary<string, IValueProvider>> BindCoreAsync(ValueBindingContext context, object value, IDictionary<string, object> parameters, bool cacheTrigger)
        {
            Dictionary<string, IValueProvider> valueProviders = new Dictionary<string, IValueProvider>();
            IValueProvider triggerProvider = null;
            IReadOnlyDictionary<string, object> bindingData = null; // TODO revert no initialize

            IValueBinder triggerReturnValueProvider= null;

            if (!cacheTrigger)
            {
                try
                {
                    // TODO For objects that we already have in the cache, we can check here and not do the binding call
                    // Also we can instrument time taken to bind here - so for objects (like Python inputStream) which we don't 
                    // wrap in the InstrumentableStream, we can gethe time it took to bind them (which includes fetching from blob storage)
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
            else
            {
                if (value.GetType() == typeof(CacheTriggeredStream))
                {
                    CacheTriggeredStream cStream = (CacheTriggeredStream)value;
                    // TODO put container here from the uri or such
                    triggerProvider = new ConstantValueProvider(value, value.GetType(), "samples-reads/"+cStream.Metadata.Name);
                    Dictionary<string, object> tempDictionary = new Dictionary<string, object>
                    {
                        { "name", cStream.Metadata.Name }
                    };
                    bindingData = new ReadOnlyDictionary<string, object>(tempDictionary);
                }
                //CacheServer cacheServer = CacheServer.Instance;
                //CacheObjectMetadata cm = new CacheObjectMetadata("https://gochaudhstorage001.blob.core.windows.net/samples-reads/ouo.txt", null);
                //if (cacheServer.ContainsObject(cm))
                //{
                //    if (cacheServer.TryGetObjectByteRangesAndStream(cm, out _, out MemoryStream ms))
                //    {
                //        triggerProvider = new ConstantValueProvider(ms, ms.GetType(), "samples-reads/ouo.txt");
                //        Dictionary<string, object> tempDictionary = new Dictionary<string, object>();
                //        tempDictionary.Add("name", "ouo.txt");
                //        bindingData = new ReadOnlyDictionary<string, object>(tempDictionary);
                //    }
                //}
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
        
        private async Task<IReadOnlyDictionary<string, InstrumentableObjectMetadata>> GetBindingDataCoreAsync(ValueBindingContext context, object value, IDictionary<string, object> parameters, bool cacheTrigger)
        {
            Dictionary<string, InstrumentableObjectMetadata> valueProviders = new Dictionary<string, InstrumentableObjectMetadata>();
            IReadOnlyDictionary<string, object> bindingData;
            InstrumentableObjectMetadata triggerMetadata = new InstrumentableObjectMetadata();
            triggerMetadata.Add("Type", "Trigger");
            triggerMetadata.Add("CacheTrigger", cacheTrigger.ToString());

            if (!cacheTrigger)
            {
                try
                {
                    ITriggerData triggerData = await _triggerBinding.BindAsync(value, context);
                    bindingData = triggerData.BindingData;
                    
                    if (bindingData.TryGetValue("Uri", out object uri))
                    {
                        triggerMetadata.Add("Uri", ((Uri)uri).ToString());
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
                            // TODO may need to clean this
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
                InstrumentableObjectMetadata nontriggerMetadata = new InstrumentableObjectMetadata();
                nontriggerMetadata.Add("Type", "NonTrigger");

                try
                {
                    ParameterDescriptor param = binding.ToParameterDescriptor();
                    if (param.GetType().ToString() == "Microsoft.Azure.WebJobs.Host.Protocols.BlobParameterDescriptor")
                    {
                        string access = param.GetType().GetProperty("Access")?.GetValue(param)?.ToString();
                        nontriggerMetadata.Add("Access", access);
                        string accountName = param.GetType().GetProperty("AccountName")?.GetValue(param)?.ToString();
                        nontriggerMetadata.Add("AccountName", accountName);
                        string blobName = param.GetType().GetProperty("BlobName")?.GetValue(param)?.ToString();
                        nontriggerMetadata.Add("Name", blobName);
                        string containerName = param.GetType().GetProperty("ContainerName")?.GetValue(param)?.ToString();
                        nontriggerMetadata.Add("ContainerName", containerName);
                    }
                }
                catch (Exception exception)
                {
                    // TODO may need to clean this, making the compiler happy :)
                    nontriggerMetadata.Add("Exception", exception.Message);
                }

                valueProviders.Add(name, nontriggerMetadata);
            }

            // TODO singleton and return value details are not yet captured
            // Need to see when exactly they are used and if we need to instrument those too

            return valueProviders;
        }      
    }
}
