using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    // Regular BindToInput has to do a TAttribute --> Value creation. 
    // But triggers already have a Listener that provided the initial object; and we're just
    // converting it ot the user's target parameter. 
    internal class TriggerHelperBindingProvider<TAttribute, TTriggerValue> :
        FluentBindingProvider<TAttribute>, IBindingProvider, IBindingRuleProvider
           where TAttribute : Attribute
            where TTriggerValue : class
    {
        private readonly INameResolver _nameResolver;
        private readonly IConverterManager _converterManager;

        public TriggerHelperBindingProvider(
          INameResolver nameResolver,
          IConverterManager converterManager
          )
        {
            this._nameResolver = nameResolver;
            this._converterManager = converterManager;
        }

        public Type GetDefaultType(Attribute attribute, FileAccess access, Type requestedType)
        {
            return typeof(object);
        }

        public IEnumerable<BindingRule> GetRules()
        {
            yield break;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var parameter = context.Parameter;
            var parameterType = parameter.ParameterType;

            if (parameterType.IsByRef)
            {
                return Task.FromResult<IBinding>(null);
            }

            var type = typeof(ExactBinding<>).MakeGenericType(typeof(TAttribute), typeof(TTriggerValue), parameterType);
            var method = type.GetMethod("TryBuild", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var binding = BindingFactoryHelpers.MethodInvoke<IBinding>(method, this, context);

            return Task.FromResult<IBinding>(binding);
        }

        private class ExactBinding<TUserType> : IBinding
        {
            private FuncConverter<TTriggerValue, TAttribute, TUserType> _converter;
            private FuncConverter<string, TAttribute, TTriggerValue> _directInvoker;

            public bool FromAttribute => true;

            public static ExactBinding<TUserType> TryBuild(
                TriggerHelperBindingProvider<TAttribute, TTriggerValue> parent,
                BindingProviderContext context)
            {
                IConverterManager cm = parent._converterManager;

                var converter = cm.GetConverter<TTriggerValue, TUserType, TAttribute>();
                if (converter == null)
                {
                    // Is there a stream composition?
                    // TTriggerValue --> Stream --> TUserType
                    var c1 = cm.GetConverter<TTriggerValue, Stream, TAttribute>();
                    if (c1 != null)
                    {
                        var c2 = cm.GetConverter<Stream, TUserType, TAttribute>();
                        if (c2 != null)
                        {
                            converter = (TTriggerValue src, TAttribute attr, ValueBindingContext ctx) =>
                            {
                                Stream o1 = c1(src, attr, ctx);
                                if (o1 ==null)
                                {
                                    return default(TUserType);
                                }
                                TUserType o2 = c2(o1, attr, ctx);
                                return o2;
                            };
                        }
                    }
                }


                if (converter == null)
                { 
                    return null;
                }





                return new ExactBinding<TUserType>
                {
                    _converter = converter,
                    _directInvoker = cm.GetConverter<string, TTriggerValue, TAttribute>()
                };
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {                
                TTriggerValue val = value as TTriggerValue;
                if (val == null)
                {
                    if (_directInvoker != null && (value is string str))
                    {
                        // Direct invoke case. NEed to converrt String-->TTriggerValue. 
                        val = _directInvoker(str, null, context);
                    }
                    else
                    {
                        // How is this possible?
                        throw new NotImplementedException();
                    }
                }

                TUserType result = _converter(val, null, context);


                string invokeString = "???";
                IValueProvider vp = new ConstantValueProvider(result, typeof(TUserType), invokeString);
                return Task.FromResult(vp);
            }

            public Task<IValueProvider> BindAsync(BindingContext context)
            {
                // Never called, since a trigger alreayd has an object. 
                throw new NotImplementedException();
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                throw new NotImplementedException();
            }

            // Caller already filterered on TAttribute


            // Get a TTriggerValue --> ParameterType conversion 
        }
    }
}
