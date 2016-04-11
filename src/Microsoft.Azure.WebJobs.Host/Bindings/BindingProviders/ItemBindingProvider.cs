using System;
using System.Threading.Tasks;
using System.Reflection;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    // Binding provider that takes an arbitrary (TAttribute ,Type) --> IValueProvider.
    // The IValueProvider  has an OnCompleted hook, so this rule can be used 
    // for bindings that have some write-back semantics and need a hook to flush. 
    internal class ItemBindingProvider<TAttribute> : IBindingProvider
        where TAttribute : Attribute
    {
        private INameResolver _nameResolver;
        private readonly Func<TAttribute, Type, Task<IValueBinder>> _builder;

        public ItemBindingProvider(
                    INameResolver nameResolver,
                    Func<TAttribute, Type, Task<IValueBinder>> builder)
        {
            this._nameResolver = nameResolver;
            this._builder = builder;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            TAttribute attributeSource = parameter.GetCustomAttribute<TAttribute>(inherit: false);

            if (attributeSource == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            var cloner = new AttributeCloner<TAttribute>(attributeSource, _nameResolver);

            IBinding binding = new Binding(cloner, _builder, parameter);
            return Task.FromResult(binding);
        }


        class Binding : BindingBase<TAttribute>
        {
            private readonly Func<TAttribute, Type, Task<IValueBinder>> _builder;
            private readonly ParameterInfo _parameter;

            public Binding(
                    AttributeCloner<TAttribute> cloner,
                    Func<TAttribute, Type, Task<IValueBinder>> builder,
                    ParameterInfo parameter
                ) : base(cloner, parameter)
            {
                this._builder = builder;
                this._parameter = parameter;
            }

            protected override async Task<IValueProvider> BuildAsync(TAttribute attrResolved)
            {
                string invokeString = _cloner.GetInvokeString(attrResolved);
                IValueBinder valueBinder = await _builder(attrResolved, _parameter.ParameterType);

                return new Wrapper { _inner = valueBinder, _invokeString = invokeString };
            }

            class Wrapper : IValueBinder
            {
                public IValueBinder _inner;
                public string _invokeString;

                public Type Type
                {
                    get
                    {
                        return _inner.Type;
                    }
                }

                public object GetValue()
                {
                    return _inner.GetValue();
                }

                public Task SetValueAsync(object value, CancellationToken cancellationToken)
                {
                    return _inner.SetValueAsync(value, cancellationToken);
                }

                public string ToInvokeString()
                {
                    return _invokeString;
                }
            }
        }
    }
}
