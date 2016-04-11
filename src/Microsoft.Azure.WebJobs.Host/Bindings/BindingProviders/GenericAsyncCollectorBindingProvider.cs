// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    // Bind Attribute --> IAsyncCollector<TMessage>, where TMessage is determined by the  user parameter type.
    // This skips the converter manager and instead dynamically allocates a generic IAsyncCollector<TMessage>
    // of the properly TMessage type. 
    internal class GenericAsyncCollectorBindingProvider<TAttribute, TConstructorArg> :
        IBindingProvider
        where TAttribute : Attribute
    {
        private readonly INameResolver _nameResolver;
        private readonly IConverterManager _converterManager;
        private readonly Type _asyncCollectorType;
        private readonly Func<TAttribute, TConstructorArg> _constructorParameterBuilder;

        public GenericAsyncCollectorBindingProvider(
            INameResolver nameResolver,
            IConverterManager converterManager,
            Type asyncCollectorType,
            Func<TAttribute, TConstructorArg> constructorParameterBuilder
            )
        {
            this._nameResolver = nameResolver;
            this._converterManager = converterManager;
            this._asyncCollectorType = asyncCollectorType;
            this._constructorParameterBuilder = constructorParameterBuilder;
        }

        // Called once per method definition. Very static. 
        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            TAttribute attribute = parameter.GetCustomAttribute<TAttribute>(inherit: false);

            if (attribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            // Now we can instantiate against the user's type.
            // throws if can't infer the type. 
            Type typeMessage = TypeUtility.GetMessageTypeFromAsyncCollector(parameter.ParameterType);

            var wrapper = WapperBase.New(
                typeMessage, _asyncCollectorType, _constructorParameterBuilder, _nameResolver, _converterManager, parameter);


            IBinding binding = wrapper.CreateBinding();
            return Task.FromResult(binding);
        }

        // Wrappers to help with binding to a dynamically typed IAsyncCollector<T>. 
        // TMessage is not known until runtime, so we need to dynamically create it. 
        // These inherit the generic args of the outer class. 
        abstract class WapperBase
        {
            protected Func<TAttribute, TConstructorArg> _constructorParameterBuilder;
            protected INameResolver _nameResolver;
            protected IConverterManager _converterManager;
            protected ParameterInfo _parameter;
            protected Type _asyncCollectorType;

            public abstract IBinding CreateBinding();

            internal static WapperBase New(
                Type typeMessage,
                Type asyncCollectorType,
                Func<TAttribute, TConstructorArg> constructorParameterBuilder,
                INameResolver nameResolver,
                IConverterManager converterManager,
                ParameterInfo parameter
                )
            {
                // These inherit the generic args of the outer class. 
                var t = typeof(Wapper<>).MakeGenericType(typeof(TAttribute), typeof(TConstructorArg), typeMessage);
                var obj = Activator.CreateInstance(t);
                var obj2 = (WapperBase)obj;

                obj2._constructorParameterBuilder = constructorParameterBuilder;
                obj2._nameResolver = nameResolver;
                obj2._converterManager = converterManager;
                obj2._parameter = parameter;
                obj2._asyncCollectorType = asyncCollectorType;

                return obj2;
            }
        }

        class Wapper<TMessage> : WapperBase
        {
            // This is the builder function that gets passed to the core IAsyncCollector binders. 
            public IAsyncCollector<TMessage> BuildFromAttribute(TAttribute attribute)
            {
                // Dynmically invoke this:
                //   TConstructorArg ctorArg = _buildFromAttr(attribute);
                //   IAsyncCollector<TMessage> collector = new MyCollector<TMessage>(ctorArg);


                var ctorArg = _constructorParameterBuilder(attribute);

                var t = _asyncCollectorType.MakeGenericType(typeof(TMessage));
                var obj = Activator.CreateInstance(t, ctorArg);
                var collector = (IAsyncCollector<TMessage>)obj;
                return collector;
            }

            public override IBinding CreateBinding()
            {
                IBinding binding = BindingFactoryHelpers.BindCollector<TAttribute, TMessage>(
                _parameter,
                _nameResolver,
                _converterManager,
                this.BuildFromAttribute, 
                null); // $$$ add hook?

                return binding;
            }
        }

    } // end class 
}