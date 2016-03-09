﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// Helpers for creating bindings to common patterns such as Messaging or Streams.
    /// This will add additional adapters to connect the user's parameter type to an IAsyncCollector. 
    /// It will also see <see cref="IConverterManager"/> to convert 
    /// from the user's type to the underlying IAsyncCollector's message type.
    /// For example, for messaging patterns, if the user is a ICollector, this will add an adapter that implements ICollector and calls IAsyncCollector.  
    /// </summary>
    public static class BindingFactory
    {
        /// <summary>
        /// Bind a  parameter to an IAsyncCollector. Use this for things that have discrete output items (like sending messages or writing table rows)
        /// This will add additional adapters to connect the user's parameter type to an IAsyncCollector. 
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="TTriggerValue"></typeparam>
        /// <param name="bindingStrategy">a strategy object that describes how to do the binding</param>
        /// <param name="parameter">the user's parameter being bound to</param>
        /// <param name="converterManager">the converter manager, used to convert between the user parameter's type and the underlying native types used by the trigger strategy</param>
        /// <param name="createListener">a function to create the underlying listener for this parameter</param>
        /// <returns></returns>
        public static ITriggerBinding GetTriggerBinding<TMessage, TTriggerValue>(
            ITriggerBindingStrategy<TMessage, TTriggerValue> bindingStrategy,
            ParameterInfo parameter,
            IConverterManager converterManager,
            Func<ListenerFactoryContext, bool, Task<IListener>> createListener)
        {
            if (bindingStrategy == null)
            {
                throw new ArgumentNullException("bindingStrategy");
            }
            if (parameter == null)
            {
                throw new ArgumentNullException("parameter");
            }

            bool singleDispatch;
            var argumentBinding = BindingFactory.GetTriggerArgumentBinding(bindingStrategy, parameter, converterManager, out singleDispatch);

            var parameterDescriptor = new ParameterDescriptor
            {
                Name = parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                    Description = singleDispatch ? "message" : "messages"
                }
            };

            ITriggerBinding binding = new CommonTriggerBinding<TMessage, TTriggerValue>(
                bindingStrategy, argumentBinding, createListener, parameterDescriptor, singleDispatch);

            return binding;
        }

        // Bind a trigger argument to various parameter types. 
        // Handles either T or T[], 
        private static ITriggerDataArgumentBinding<TTriggerValue> GetTriggerArgumentBinding<TMessage, TTriggerValue>(
            ITriggerBindingStrategy<TMessage, TTriggerValue> bindingStrategy, 
            ParameterInfo parameter, 
            IConverterManager converterManager,
            out bool singleDispatch)
        {        
            ITriggerDataArgumentBinding<TTriggerValue> argumentBinding = null;
            if (parameter.ParameterType.IsArray)
            {
                // dispatch the entire batch in a single call. 
                singleDispatch = false;

                var elementType = parameter.ParameterType.GetElementType();

                var innerArgumentBinding = GetTriggerArgumentElementBinding<TMessage, TTriggerValue>(elementType, bindingStrategy, converterManager);

                argumentBinding = new ArrayTriggerArgumentBinding<TMessage, TTriggerValue>(bindingStrategy, innerArgumentBinding, converterManager);

                return argumentBinding;
            }
            else
            {
                // Dispatch each item one at a time
                singleDispatch = true;

                var elementType = parameter.ParameterType;
                argumentBinding = GetTriggerArgumentElementBinding<TMessage, TTriggerValue>(elementType, bindingStrategy, converterManager);
                return argumentBinding;
            }
        }
        
        // Bind a T. 
        private static SimpleTriggerArgumentBinding<TMessage, TTriggerValue> GetTriggerArgumentElementBinding<TMessage, TTriggerValue>(
            Type elementType, 
            ITriggerBindingStrategy<TMessage, TTriggerValue> bindingStrategy,
            IConverterManager converterManager)
        {
            if (elementType == typeof(TMessage))
            {
                return new SimpleTriggerArgumentBinding<TMessage, TTriggerValue>(bindingStrategy, converterManager);
            }
            if (elementType == typeof(string))
            {
                return new StringTriggerArgumentBinding<TMessage, TTriggerValue>(bindingStrategy, converterManager);
            }
            else
            {
                // Default, assume a Poco
                return new PocoTriggerArgumentBinding<TMessage, TTriggerValue>(bindingStrategy, converterManager, elementType);
            }         
        }

        /// <summary>
        /// Bind a  parameter to an IAsyncCollector. Use this for things that have discrete output items (like sending messages or writing table rows)
        /// This will add additional adapters to connect the user's parameter type to an IAsyncCollector. 
        /// </summary>
        /// <typeparam name="TMessage">The native type of message. For example, for azure queues, this would be CloudQueueMessage.</typeparam>
        /// <typeparam name="TContext">helper object to pass to the binding the configuration state. This can point back to context like secrets, configuration, etc.</typeparam>
        /// <param name="parameter">parameter being bound.</param>
        /// <param name="converterManager"></param>
        /// <param name="builder">function to create a new instance of the underlying Collector object to pass to the parameter. 
        /// This binder will wrap that in any adapters to make it match the requested parameter type.</param>
        /// <param name="invokeString">a short string summary that describes this binding. That will display on the dashboard</param>
        /// <param name="invokeStringBinder">retrieve a context from the invoke string. </param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "string")]
        public static IBinding BindCollector<TMessage, TContext>(
            ParameterInfo parameter,
            IConverterManager converterManager,
            Func<TContext, ValueBindingContext, IAsyncCollector<TMessage>> builder,
            string invokeString,
            Func<string, TContext> invokeStringBinder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (invokeStringBinder == null)
            {
                throw new ArgumentNullException("invokeStringBinder");
            }
            if (parameter == null)
            {
                throw new ArgumentNullException("parameter");
            }

            Type parameterType = parameter.ParameterType;

            Func<TContext, ValueBindingContext, IValueProvider> argumentBuilder = null;            

            if (parameterType.IsGenericType)
            {
                var genericType = parameterType.GetGenericTypeDefinition();
                var elementType = parameterType.GetGenericArguments()[0];

                if (genericType == typeof(IAsyncCollector<>))
                {
                    if (elementType == typeof(TMessage))
                    {
                        // Bind to IAsyncCollector<TMessage>. This is the "purest" binding, no adaption needed. 
                        argumentBuilder = (context, valueBindingContext) =>
                        {
                            IAsyncCollector<TMessage> raw = builder(context, valueBindingContext);
                            return new AsyncCollectorValueProvider<IAsyncCollector<TMessage>, TMessage>(raw, raw, invokeString);
                        };
                    }
                    else
                    {
                        // Bind to IAsyncCollector<T>
                        // Get a converter from T to TMessage
                        argumentBuilder = DynamicInvokeBuildIAsyncCollectorArgument(elementType, converterManager, builder, invokeString);
                    }
                }
                else if (genericType == typeof(ICollector<>))
                {
                    if (elementType == typeof(TMessage))
                    {
                        // Bind to ICollector<TMessage> This just needs an Sync/Async wrapper
                        argumentBuilder = (context, valueBindingContext) =>
                        {
                            IAsyncCollector<TMessage> raw = builder(context, valueBindingContext);
                            ICollector<TMessage> obj = new SyncAsyncCollectorAdapter<TMessage>(raw);
                            return new AsyncCollectorValueProvider<ICollector<TMessage>, TMessage>(obj, raw, invokeString);
                        };
                    }
                    else
                    {
                        // Bind to ICollector<T>. 
                        // This needs both a conversion from T to TMessage and an Sync/Async wrapper
                        argumentBuilder = DynamicInvokeBuildICollectorArgument(elementType, converterManager, builder, invokeString);
                    }
                }
            }

            if (parameter.IsOut)
            {
                Type elementType = parameter.ParameterType.GetElementType();

                if (elementType.IsArray)
                {
                    if (elementType == typeof(TMessage[]))
                    {
                        argumentBuilder = (context, valueBindingContext) =>
                        {
                            IAsyncCollector<TMessage> raw = builder(context, valueBindingContext);
                            return new OutArrayValueProvider<TMessage>(raw, invokeString);
                        };
                    }
                    else
                    {
                        // out TMessage[]
                        var e2 = elementType.GetElementType();
                        argumentBuilder = DynamicBuildOutArrayArgument(e2, converterManager, builder, invokeString);
                    }
                }
                else
                {
                    // Single enqueue
                    //    out TMessage
                    if (elementType == typeof(TMessage))
                    {
                        argumentBuilder = (context, valueBindingContext) =>
                        {
                            IAsyncCollector<TMessage> raw = builder(context, valueBindingContext);
                            return new OutValueProvider<TMessage>(raw, invokeString);
                        };
                    }
                    else
                    {
                        // use JSon converter
                        // out T
                        argumentBuilder = DynamicInvokeBuildOutArgument(elementType, converterManager, builder, invokeString);
                    }
                }
            }

            if (argumentBuilder != null)
            {
                ParameterDescriptor param = new ParameterDescriptor
                {
                    Name = parameter.Name,
                    DisplayHints = new ParameterDisplayHints
                    {
                        Description = "output"
                    }
                };

                var initialClient = invokeStringBinder(invokeString);
                return new AsyncCollectorBinding<TMessage, TContext>(initialClient, argumentBuilder, param, invokeStringBinder);
            }

            string msg = string.Format(CultureInfo.CurrentCulture, "Can't bind to {0}.", parameter);
            throw new InvalidOperationException(msg);
        }

        // Helper to dynamically invoke BuildICollectorArgument with the proper generics
        private static Func<TContext, ValueBindingContext, IValueProvider> DynamicBuildOutArrayArgument<TContext, TMessage>(
                Type typeMessageSrc,
                IConverterManager cm,
                Func<TContext, ValueBindingContext, IAsyncCollector<TMessage>> builder,
                string invokeString)
        {
            var method = typeof(BindingFactory).GetMethod("BuildOutArrayArgument", BindingFlags.NonPublic | BindingFlags.Static);
            method = method.MakeGenericMethod(typeof(TContext), typeMessageSrc, typeof(TMessage));
            var argumentBuilder = (Func<TContext, ValueBindingContext, IValueProvider>)
            method.Invoke(null, new object[] { cm, builder, invokeString });
            return argumentBuilder;
        }

        private static Func<TContext, ValueBindingContext, IValueProvider> BuildOutArrayArgument<TContext, TMessageSrc, TMessage>(
            IConverterManager cm,
            Func<TContext, ValueBindingContext, IAsyncCollector<TMessage>> builder,
            string invokeString)
        {
            // Other 
            Func<TMessageSrc, TMessage> convert = cm.GetConverter<TMessageSrc, TMessage>();
            Func<TContext, ValueBindingContext, IValueProvider> argumentBuilder = (context, valueBindingContext) =>
            {
                IAsyncCollector<TMessage> raw = builder(context, valueBindingContext);
                IAsyncCollector<TMessageSrc> obj = new TypedAsyncCollectorAdapter<TMessageSrc, TMessage>(raw, convert);

                return new OutArrayValueProvider<TMessageSrc>(obj, invokeString);
            };
            return argumentBuilder;
        }
        
        // Helper to dynamically invoke BuildICollectorArgument with the proper generics
        private static Func<TContext, ValueBindingContext, IValueProvider> DynamicInvokeBuildOutArgument<TContext, TMessage>(
                Type typeMessageSrc,
                IConverterManager cm,
                Func<TContext, ValueBindingContext, IAsyncCollector<TMessage>> builder,
                string invokeString)
        {
            var method = typeof(BindingFactory).GetMethod("BuildOutArgument", BindingFlags.NonPublic | BindingFlags.Static);
            method = method.MakeGenericMethod(typeof(TContext), typeMessageSrc, typeof(TMessage));
            var argumentBuilder = (Func<TContext, ValueBindingContext, IValueProvider>)
            method.Invoke(null, new object[] { cm, builder, invokeString });
            return argumentBuilder;
        }

        private static Func<TContext, ValueBindingContext, IValueProvider> BuildOutArgument<TContext, TMessageSrc, TMessage>(
            IConverterManager cm,
            Func<TContext, ValueBindingContext, IAsyncCollector<TMessage>> builder,
            string invokeString)
        {
            // Other 
            Func<TMessageSrc, TMessage> convert = cm.GetConverter<TMessageSrc, TMessage>();
            Func<TContext, ValueBindingContext, IValueProvider> argumentBuilder = (context, valueBindingContext) =>
            {
                IAsyncCollector<TMessage> raw = builder(context, valueBindingContext);
                IAsyncCollector<TMessageSrc> obj = new TypedAsyncCollectorAdapter<TMessageSrc, TMessage>(raw, convert);
                return new OutValueProvider<TMessageSrc>(obj, invokeString);
            };
            return argumentBuilder;
        }

        // Helper to dynamically invoke BuildICollectorArgument with the proper generics
        private static Func<TContext, ValueBindingContext, IValueProvider> DynamicInvokeBuildICollectorArgument<TContext, TMessage>(
                Type typeMessageSrc,
                IConverterManager cm,
                Func<TContext, ValueBindingContext, IAsyncCollector<TMessage>> builder, 
                string invokeString)
        {
            var method = typeof(BindingFactory).GetMethod("BuildICollectorArgument", BindingFlags.NonPublic | BindingFlags.Static);
            method = method.MakeGenericMethod(typeof(TContext), typeMessageSrc, typeof(TMessage));
            var argumentBuilder = (Func<TContext, ValueBindingContext, IValueProvider>)
            method.Invoke(null, new object[] { cm, builder, invokeString });
            return argumentBuilder;
        }

        private static Func<TContext, ValueBindingContext, IValueProvider> BuildICollectorArgument<TContext, TMessageSrc, TMessage>(
            IConverterManager cm,
            Func<TContext, ValueBindingContext, IAsyncCollector<TMessage>> builder,
            string invokeString)
        {
            // Other 
            Func<TMessageSrc, TMessage> convert = cm.GetConverter<TMessageSrc, TMessage>();
            Func<TContext, ValueBindingContext, IValueProvider> argumentBuilder = (context, valueBindingContext) =>
            {
                IAsyncCollector<TMessage> raw = builder(context, valueBindingContext);
                IAsyncCollector<TMessageSrc> obj = new TypedAsyncCollectorAdapter<TMessageSrc, TMessage>(raw, convert);
                ICollector<TMessageSrc> obj2 = new SyncAsyncCollectorAdapter<TMessageSrc>(obj);
                return new AsyncCollectorValueProvider<ICollector<TMessageSrc>, TMessage>(obj2, raw, invokeString);
            };
            return argumentBuilder;
        }

        // Helper to dynamically invoke BuildIAsyncCollectorArgument with the proper generics
        private static Func<TContext, ValueBindingContext, IValueProvider> DynamicInvokeBuildIAsyncCollectorArgument<TContext, TMessage>(
                Type typeMessageSrc,
                IConverterManager cm,
                Func<TContext, ValueBindingContext, IAsyncCollector<TMessage>> builder,
                string invokeString)
        {
            var method = typeof(BindingFactory).GetMethod("BuildIAsyncCollectorArgument", BindingFlags.NonPublic | BindingFlags.Static);
            method = method.MakeGenericMethod(typeof(TContext), typeMessageSrc, typeof(TMessage));
            var argumentBuilder = (Func<TContext, ValueBindingContext, IValueProvider>)
            method.Invoke(null, new object[] { cm, builder, invokeString });
            return argumentBuilder;
        }

        // Helper to build an argument binder for IAsyncCollector<TMessageSrc>
        private static Func<TContext, ValueBindingContext, IValueProvider> BuildIAsyncCollectorArgument<TContext, TMessageSrc, TMessage>(
            IConverterManager cm,
            Func<TContext, ValueBindingContext, IAsyncCollector<TMessage>> builder,
            string invokeString)
        {
            Func<TMessageSrc, TMessage> convert = cm.GetConverter<TMessageSrc, TMessage>();
            Func<TContext, ValueBindingContext, IValueProvider> argumentBuilder = (context, valueBindingContext) =>
            {
                IAsyncCollector<TMessage> raw = builder(context, valueBindingContext);
                IAsyncCollector<TMessageSrc> obj = new TypedAsyncCollectorAdapter<TMessageSrc, TMessage>(raw, convert);
                return new AsyncCollectorValueProvider<IAsyncCollector<TMessageSrc>, TMessage>(obj, raw, invokeString);
            };
            return argumentBuilder;
        }    
    }
}