// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.ServiceBus.Messaging;
using Microsoft.Azure.WebJobs.Host.Protocols;
using System.Globalization;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // Bind to an IAsyncCollector
    internal class GenericBinder
    {
        // Bind an IAsyncCollector<TMessage> to a user parameter. 
        // This handles the various flavors of parameter types and will morph them to the connector.
        // parameter  - parameter being bound. 
        // TContext - helper object to pass to the binding the configuration state. This can point back to context like secrets, configuration, etc.
        // builder - function to create a new instance of the underlying Collector object to pass to the parameter. 
        //          This binder will wrap that in any adpaters to make it match the requested parameter type.
        public static IBinding BindCollector<TMessage, TContext>(
            ParameterInfo parameter,
            TContext client,
            Func<TContext, ValueBindingContext, IFlushCollector<TMessage>> builder)
        {
            ConverterManager cm = new ConverterManager();

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
                            IFlushCollector<TMessage> raw = builder(context, valueBindingContext);
                            return new CommonAsyncCollectorValueProvider<IAsyncCollector<TMessage>, TMessage>(raw, raw);
                        };
                    }
                    else {
                        // Bind to IAsyncCollector<T>
                        // Get a converter from T to TMessage
                        argumentBuilder = DynamicInvokeBuildIAsyncCollectorArgument(elementType, cm, builder);
                    }
                }
                else if (genericType == typeof(ICollector<>))
                {
                    if (elementType == typeof(TMessage))
                    {
                        // Bind to ICollector<TMessage> This just needs an Sync/Async wrapper
                        argumentBuilder = (context, valueBindingContext) =>
                        {
                            IFlushCollector<TMessage> raw = builder(context, valueBindingContext);
                            ICollector<TMessage> obj = new SyncAsyncCollectorAdapter<TMessage>(raw);
                            return new CommonAsyncCollectorValueProvider<ICollector<TMessage>, TMessage>(obj, raw);
                        };
                    }
                    else
                    {
                        // Bind to ICollector<T>. 
                        // This needs both a conversion from T to TMessage and an Sync/Async wrapper
                        argumentBuilder = DynamicInvokeBuildICollectorArgument(elementType, cm, builder);
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
                            IFlushCollector<TMessage> raw = builder(context, valueBindingContext);
                            return new OutArrayValueProvider<TMessage>(raw);
                        };
                    }

                    // out TMessage[]
                    var e2 = elementType.GetElementType();
                }
                else
                {
                    // Single enqueue
                    //    out TMessage
                    if (elementType == typeof(TMessage))
                    {
                        argumentBuilder = (context, valueBindingContext) =>
                        {
                            IFlushCollector<TMessage> raw = builder(context, valueBindingContext);
                            return new OutValueProvider<TMessage>(raw);
                        };
                    }
                    else
                    {
                        // use JSon converter
                        // out T
                        argumentBuilder = DynamicInvokeBuildOutArgument(elementType, cm, builder);
                    }
                }
            }

            if (argumentBuilder != null)
            {
                ParameterDescriptor param = new ParameterDescriptor { Name = parameter.Name };
                return new GenericCollectorBinding<TMessage, TContext>(client, argumentBuilder, param);
            }

            string msg = string.Format("Can't bind to {0}.", parameter);
            throw new InvalidOperationException(msg);
        }


        // Helper to dynamically invoke BuildICollectorArgument with the proper generics
        static Func<TContext, ValueBindingContext, IValueProvider> DynamicInvokeBuildOutArgument<TContext, TMessage>(
                Type typeMessageSrc,
                ConverterManager cm,
                Func<TContext, ValueBindingContext, IFlushCollector<TMessage>> builder)
        {
            var method = typeof(GenericBinder).GetMethod("BuildOutArgument", BindingFlags.NonPublic | BindingFlags.Static);
            method = method.MakeGenericMethod(typeof(TContext), typeMessageSrc, typeof(TMessage));
            var argumentBuilder = (Func<TContext, ValueBindingContext, IValueProvider>)
            method.Invoke(null, new object[] { cm, builder });
            return argumentBuilder;
        }

        static Func<TContext, ValueBindingContext, IValueProvider> BuildOutArgument<TContext, TMessageSrc, TMessage>(
            ConverterManager cm,
            Func<TContext, ValueBindingContext, IFlushCollector<TMessage>> builder
            )
        {
            // Other 
            Func<TMessageSrc, TMessage> convert = cm.GetConverter<TMessageSrc, TMessage>();
            Func<TContext, ValueBindingContext, IValueProvider> argumentBuilder = (context, valueBindingContext) =>
            {
                IFlushCollector<TMessage> raw = builder(context, valueBindingContext);
                IFlushCollector<TMessageSrc> obj = new TypedAsyncCollectorAdapter<TMessageSrc, TMessage>(raw, convert);
                return new OutValueProvider<TMessageSrc>(obj);
            };
            return argumentBuilder;
        }



        // Helper to dynamically invoke BuildICollectorArgument with the proper generics
        static Func<TContext, ValueBindingContext, IValueProvider> DynamicInvokeBuildICollectorArgument<TContext, TMessage>(
                Type typeMessageSrc,
                ConverterManager cm,
                Func<TContext, ValueBindingContext, IFlushCollector<TMessage>> builder)
        {
            var method = typeof(GenericBinder).GetMethod("BuildICollectorArgument", BindingFlags.NonPublic | BindingFlags.Static);
            method = method.MakeGenericMethod(typeof(TContext), typeMessageSrc, typeof(TMessage));
            var argumentBuilder = (Func<TContext, ValueBindingContext, IValueProvider>)
            method.Invoke(null, new object[] { cm, builder });
            return argumentBuilder;
        }

        static Func<TContext, ValueBindingContext, IValueProvider> BuildICollectorArgument<TContext, TMessageSrc, TMessage>(
            ConverterManager cm,
            Func<TContext, ValueBindingContext, IFlushCollector<TMessage>> builder
            )
        {
            // Other 
            Func<TMessageSrc, TMessage> convert = cm.GetConverter<TMessageSrc, TMessage>();
            Func<TContext, ValueBindingContext, IValueProvider> argumentBuilder = (context, valueBindingContext) =>
            {
                IFlushCollector<TMessage> raw = builder(context, valueBindingContext);
                IAsyncCollector<TMessageSrc> obj = new TypedAsyncCollectorAdapter<TMessageSrc, TMessage>(raw, convert);
                ICollector<TMessageSrc> obj2 = new SyncAsyncCollectorAdapter<TMessageSrc>(obj);
                return new CommonAsyncCollectorValueProvider<ICollector<TMessageSrc>, TMessage>(obj2, raw);
            };
            return argumentBuilder;
        }




        // Helper to dynamically invoke BuildIAsyncCollectorArgument with the proper generics
        static Func<TContext, ValueBindingContext, IValueProvider> DynamicInvokeBuildIAsyncCollectorArgument<TContext, TMessage>(
                Type typeMessageSrc,
                ConverterManager cm,
                Func<TContext, ValueBindingContext, IFlushCollector<TMessage>> builder)
        {
            var method = typeof(GenericBinder).GetMethod("BuildIAsyncCollectorArgument", BindingFlags.NonPublic | BindingFlags.Static);
            method = method.MakeGenericMethod(typeof(TContext), typeMessageSrc, typeof(TMessage));
            var argumentBuilder = (Func<TContext, ValueBindingContext, IValueProvider>)
            method.Invoke(null, new object[] { cm, builder });
            return argumentBuilder;
        }

        // Helper to build an argument binder for IAsyncCollector<TMessageSrc>
        static Func<TContext, ValueBindingContext, IValueProvider> BuildIAsyncCollectorArgument<TContext, TMessageSrc, TMessage>(
            ConverterManager cm,
            Func<TContext, ValueBindingContext, IFlushCollector<TMessage>> builder
            )
        {
            Func<TMessageSrc, TMessage> convert = cm.GetConverter<TMessageSrc, TMessage>();
            Func<TContext, ValueBindingContext, IValueProvider> argumentBuilder = (context, valueBindingContext) =>
            {
                IFlushCollector<TMessage> raw = builder(context, valueBindingContext);
                IAsyncCollector<TMessageSrc> obj = new TypedAsyncCollectorAdapter<TMessageSrc, TMessage>(raw, convert);
                return new CommonAsyncCollectorValueProvider<IAsyncCollector<TMessageSrc>, TMessage>(obj, raw);
            };
            return argumentBuilder;
        }

        // This binding is static per parameter, and then called on each invocation 
        // to produce a new parameter instance. 
        internal class GenericCollectorBinding<TMessage, TContext> : IBinding
        {
            private readonly TContext _client;
            private readonly ParameterDescriptor _param;
            private readonly Func<TContext, ValueBindingContext, IValueProvider> _argumentBuilder;

            public GenericCollectorBinding(
                TContext client,
                Func<TContext, ValueBindingContext, IValueProvider> argumentBuilder,
                ParameterDescriptor param
                )
            {
                this._client = client;
                this._argumentBuilder = argumentBuilder;
                this._param = param;
            }

            public bool FromAttribute
            {
                get
                {
                    return true;
                }
            }

            public Task<IValueProvider> BindAsync(BindingContext context)
            {
                return BindAsync(null, context.ValueContext);
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                IValueProvider valueProvider = _argumentBuilder(_client, context);
                return Task.FromResult(valueProvider);
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return _param;
            }
        }
    }
}