// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    // General rule for binding parameters to an AsyncCollector. 
    // Supports the various flavors like IAsyncCollector, ICollector, out T, out T[]. 
    internal class AsyncCollectorBindingProvider<TAttribute, TType> : FluidBindingProvider<TAttribute>, IBindingProvider
        where TAttribute : Attribute
    {
        private readonly INameResolver _nameResolver;
        private readonly IConverterManager _converterManager;
        private readonly PatternMatcher _patternMatcher;

         public AsyncCollectorBindingProvider(
            INameResolver nameResolver,
            IConverterManager converterManager,
            PatternMatcher patternMatcher)
        {
            this._nameResolver = nameResolver;
            this._converterManager = converterManager;
            this._patternMatcher = patternMatcher;
        }

        // Describe different flavors of IAsyncCollector<T> bindings. 
        private enum Mode
        {
            IAsyncCollector,
            ICollector,
            OutSingle,
            OutArray
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var parameter = context.Parameter;

            var mode = GetMode(parameter);
            if (mode == null)
            {
                return Task.FromResult<IBinding>(null);
            }
            
            var type = typeof(ExactBinding<>).MakeGenericType(typeof(TAttribute), typeof(TType), mode.ElementType);
            var method = type.GetMethod("TryBuild", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var binding = BindingFactoryHelpers.MethodInvoke<IBinding>(method, this, mode.Mode, context);

            return Task.FromResult<IBinding>(binding);
        }

        // Parse the signature to determine which mode this is. 
        // Can also check with converter manager to disambiguate some cases. 
        private BindingMode GetMode(ParameterInfo parameter)
        {
            Type parameterType = parameter.ParameterType;
            if (parameterType.IsGenericType)
            {
                var genericType = parameterType.GetGenericTypeDefinition();
                var elementType = parameterType.GetGenericArguments()[0];

                if (genericType == typeof(IAsyncCollector<>))
                {
                    return new BindingMode(Mode.IAsyncCollector, elementType);
                }
                else if (genericType == typeof(ICollector<>))
                {
                    return new BindingMode(Mode.ICollector, elementType);
                }

                // A different interface. Let another rule try it. 
                return null;
            }

            if (parameter.IsOut)
            {
                // How should "out byte[]" bind?
                // If there's an explicit "byte[] --> TMessage" converter, then that takes precedence.                 
                // Else, bind over an array of "byte --> TMessage" converters 
                Type elementType = parameter.ParameterType.GetElementType();
                bool hasConverter = TypeUtility.HasConverter<TAttribute>(this._converterManager, elementType, typeof(TType));
                if (hasConverter)
                {
                    // out T, where T might be an array 
                    return new BindingMode(Mode.OutSingle, elementType);
                }

                if (elementType.IsArray)
                {
                    // out T[]
                    var messageType = elementType.GetElementType();
                    return new BindingMode(Mode.OutArray, messageType);
                }

                var checker = ConverterManager.GetTypeValidator<TType>();
                if (checker.IsMatch(elementType))
                {
                    // out T, t is not an array 
                    return new BindingMode(Mode.OutSingle, elementType);                    
                }

                // For out-param ,we don't expect another rule to claim it. So give some rich errors on mismatch.
                if (typeof(IEnumerable).IsAssignableFrom(elementType))
                {
                    throw new InvalidOperationException(
                        "Enumerable types are not supported. Use ICollector<T> or IAsyncCollector<T> instead.");
                }
                else if (typeof(object) == elementType)
                {
                    throw new InvalidOperationException("Object element types are not supported.");
                }
            }

            // No match. Let another rule claim it
            return null;            
        }

        // Represent the different possible flavors for binding to an async collector
        private class BindingMode
        {
            public BindingMode(Mode mode, Type elementType)
            {
                this.Mode = mode;
                this.ElementType = elementType;
            }
            public Mode Mode { get; set; }
            public Type ElementType { get; set; }
        }

        // TType - specified in the rule. 
        // TMessage - extracted from the user's parameter. 
        private class ExactBinding<TMessage> : BindingBase<TAttribute>
        {
            private readonly Func<object, object> _buildFromAttribute;

            private readonly FuncConverter<TMessage, TAttribute, TType> _converter;
            private readonly Mode _mode;

            public ExactBinding(
                AttributeCloner<TAttribute> cloner,
                ParameterDescriptor param,
                Mode mode,
                Func<object, object> buildFromAttribute,
                FuncConverter<TMessage, TAttribute, TType> converter) : base(cloner, param)
            {
                this._buildFromAttribute = buildFromAttribute;
                this._mode = mode;
                this._converter = converter;
            }

            public static ExactBinding<TMessage> TryBuild(
                AsyncCollectorBindingProvider<TAttribute, TType> parent,
                Mode mode,
                BindingProviderContext context)
            {
                var cm = parent._converterManager;
                var patternMatcher = parent._patternMatcher;

                var parameter = context.Parameter;
                TAttribute attributeSource = parameter.GetCustomAttribute<TAttribute>(inherit: false);

                Func<TAttribute, Task<TAttribute>> hookWrapper = null;
                if (parent.PostResolveHook != null)
                {
                    hookWrapper = (attrResolved) => parent.PostResolveHook(attrResolved, parameter, parent._nameResolver);
                }

                var cloner = new AttributeCloner<TAttribute>(attributeSource, context.BindingDataContract, parent._nameResolver, hookWrapper);

                Func<object, object> buildFromAttribute;
                FuncConverter<TMessage, TAttribute, TType> converter = null;

                // Prefer the shortest route to creating the user type.
                // If TType matches the user type directly, then we should be able to directly invoke the builder in a single step. 
                //   TAttribute --> TUserType
                var checker = ConverterManager.GetTypeValidator<TType>();
                if (checker.IsMatch(typeof(TMessage)))
                {
                    buildFromAttribute = patternMatcher.TryGetConverterFunc(
                        typeof(TAttribute), typeof(IAsyncCollector<TMessage>));
                }
                else
                {
                    // Try with a converter
                    // Find a builder for :   TAttribute --> TType
                    // and then couple with a converter:  TType --> TParameterType
                    converter = cm.GetConverter<TMessage, TType, TAttribute>();
                    if (converter == null)
                    {
                        // Preserves legacy behavior. This means we can only have 1 async collector.
                        // However, the collector's builder object can switch. 
                        throw NewMissingConversionError(typeof(TMessage));
                    }

                    buildFromAttribute = patternMatcher.TryGetConverterFunc(
                        typeof(TAttribute), typeof(IAsyncCollector<TType>));
                }

                if (buildFromAttribute == null)
                {
                    return null;
                }

                ParameterDescriptor param;
                if (parent.BuildParameterDescriptor != null)
                {
                    param = parent.BuildParameterDescriptor(attributeSource, parameter, parent._nameResolver);
                }
                else
                {
                    param = new ParameterDescriptor
                    {
                        Name = parameter.Name,
                        DisplayHints = new ParameterDisplayHints
                        {
                            Description = "input"
                        }
                    };
                }

                return new ExactBinding<TMessage>(cloner, param, mode, buildFromAttribute, converter);
            }

            // typeUser - type in the user's parameter. 
            private static Exception NewMissingConversionError(Type typeUser)
            {
                if (typeUser.IsPrimitive)
                {
                    return new NotSupportedException("Primitive types are not supported.");
                }

                if (typeof(IEnumerable).IsAssignableFrom(typeUser))
                {
                    return new InvalidOperationException("Nested collections are not supported.");
                }
                return new InvalidOperationException("Can't convert from type '" + typeUser.FullName);
            }

            protected override Task<IValueProvider> BuildAsync(
                TAttribute attrResolved,
                ValueBindingContext context)
            {
                string invokeString = Cloner.GetInvokeString(attrResolved);

                object obj = _buildFromAttribute(attrResolved);
                
                IAsyncCollector<TMessage> collector;
                if (_converter != null)
                {
                    // Apply a converter
                    var collector2 = (IAsyncCollector<TType>)obj;

                    collector = new TypedAsyncCollectorAdapter<TMessage, TType, TAttribute>(
                                collector2, _converter, attrResolved, context);
                }
                else
                {
                    collector = (IAsyncCollector<TMessage>)obj;
                }

                var vp = CoerceValueProvider(_mode, invokeString, collector);
                return Task.FromResult(vp);
            }

            // Get a ValueProvider that's in the right mode. 
            private static IValueProvider CoerceValueProvider(Mode mode, string invokeString, IAsyncCollector<TMessage> collector)
            {
                IValueProvider vp;

                switch (mode)
                {
                    case Mode.IAsyncCollector:
                        vp = new AsyncCollectorValueProvider<IAsyncCollector<TMessage>, TMessage>(collector, collector, invokeString);
                        return vp;

                    case Mode.ICollector:
                        ICollector<TMessage> syncCollector = new SyncAsyncCollectorAdapter<TMessage>(collector);
                        vp = new AsyncCollectorValueProvider<ICollector<TMessage>, TMessage>(syncCollector, collector, invokeString);
                        return vp;

                    case Mode.OutArray:
                        return new OutArrayValueProvider<TMessage>(collector, invokeString);
                        
                    case Mode.OutSingle:
                        return new OutValueProvider<TMessage>(collector, invokeString);
                        
                    default:
                        throw new NotImplementedException($"mode ${mode} not implemented");
                }             
            }
        }
    }
}
