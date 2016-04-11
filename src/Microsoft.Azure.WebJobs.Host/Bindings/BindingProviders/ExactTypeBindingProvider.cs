// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    // BindingProvider to bind to an exact type. Useful for binding to a client object. 
    internal class ExactTypeBindingProvider<TAttribute, TUserType> : IBindingProvider
        where TAttribute : Attribute
    {
        private Func<TAttribute, Task<TUserType>> _buildFromAttr;
        private INameResolver _nameResolver;
        private readonly Func<TAttribute, ParameterInfo, INameResolver, Task<TAttribute>> _postResolveHook;

        public ExactTypeBindingProvider(
            INameResolver nameResolver,
            Func<TAttribute, Task<TUserType>> buildFromAttr,
            Func<TAttribute, ParameterInfo, INameResolver, Task<TAttribute>> postResolveHook
            )
        {
            this._postResolveHook = postResolveHook;
            this._nameResolver = nameResolver;
            this._buildFromAttr = buildFromAttr;
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

            if (parameter.ParameterType != typeof(TUserType))
            {
                return Task.FromResult<IBinding>(null);
            }

            Func<TAttribute, Task<TAttribute>> hookWrapper = null;
            if (_postResolveHook != null)
            {
                hookWrapper = (attrResolved) => _postResolveHook(attrResolved, parameter, _nameResolver);
            }

            var cloner = new AttributeCloner<TAttribute>(attributeSource, _nameResolver, hookWrapper);
            ParameterDescriptor param = new ParameterDescriptor
            {
                Name = parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                    Description = "output"
                }
            };

            var binding = new ExactBinding(cloner, param, _buildFromAttr);
            return Task.FromResult<IBinding>(binding);
        }


        class ExactBinding : BindingBase<TAttribute>
        {
            internal Func<TAttribute, Task<TUserType>> _buildFromAttr;

            public ExactBinding(
                AttributeCloner<TAttribute> cloner, 
                ParameterDescriptor param, 
                Func<TAttribute, Task<TUserType>> buildFromAttr)
                : base(cloner, param)
            {
                _buildFromAttr = buildFromAttr;
            }

            protected override async Task<IValueProvider> BuildAsync(TAttribute attrResolved)
            {
                string invokeString = _cloner.GetInvokeString(attrResolved);
                var obj = await _buildFromAttr(attrResolved);

                IValueProvider vp = new ConstantValueProvider(obj, typeof(TUserType), invokeString);
                return vp;
            }            
        }
    }
}