// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Bindings;
using System.Reflection;
using System.Collections.ObjectModel;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

namespace SampleHost
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Register extensions
            var config = new JobHostConfiguration();
            config.Queues.VisibilityTimeout = TimeSpan.FromSeconds(15);
            config.Queues.MaxDequeueCount = 3;

            if (config.IsDevelopment)
            {
                config.UseDevelopmentSettings();
            }

            
            // 2. Get the Graph (binding providers, converters).
            IBindingProvider provider = config.GetBindingProvider();

            // Script will probe to figure out DefaultType for extension bindings. 
            var attr = new BlobAttribute("container/path", FileAccess.Read);
            var ok = CanBind(provider, attr, typeof(TextReader)).Result;
                        
            // 3. Script can update Types pointed by TypeLocator

            // 4. Now spin up host for execution 
            var host = new JobHost(config);

            var m = typeof(Functions).GetMethod("QueueOutput");
            host.Call(m);

            host.RunAndBlock();
        }

        static async Task<bool> CanBind(IBindingProvider provider, Attribute attribute, Type t)
        {
            ParameterInfo parameterInfo = new FakeParameterInfo(
                t, 
                new FakeMemberInfo(), 
                attribute, 
                null);

            BindingProviderContext bindingProviderContext = new BindingProviderContext(
                parameterInfo, bindingDataContract: null, cancellationToken: CancellationToken.None);

            try
            {
                var binding = await provider.TryCreateAsync(bindingProviderContext);
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }
    }

    class FakeMemberInfo : MemberInfo
    {
        public override Type DeclaringType
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override MemberTypes MemberType
        {
            get
            {
                return MemberTypes.All;
            }
        }

        public override string Name
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override Type ReflectedType
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            throw new NotImplementedException();
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }
    }

    // A non-reflection based implementation
    class FakeParameterInfo : ParameterInfo
    {
        private readonly Collection<Attribute> _attributes = new Collection<Attribute>();

        public FakeParameterInfo(Type parameterType, MemberInfo memberInfo, Attribute attribute, Attribute[] additionalAttributes)
        {
            ClassImpl = parameterType;
            AttrsImpl = ParameterAttributes.In;
            NameImpl = "?";
            MemberImpl = memberInfo;

            // union all the parameter attributes
            _attributes.Add(attribute);          
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return _attributes.Where(p => p.GetType() == attributeType).ToArray();
        }
    }
}
