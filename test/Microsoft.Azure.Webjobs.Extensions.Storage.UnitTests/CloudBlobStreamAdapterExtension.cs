// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Config;
using System.IO;
using System.Threading;
using System.Linq;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    // Extension to provide legacy ICloudBlobStreamBinder support. 
    // Registers them as converts. 
    class CloudBlobStreamAdapterExtension : IExtensionConfigProvider
    {
        private readonly Type[] _cloudBlobStreamBinderTypes;
        public CloudBlobStreamAdapterExtension(Type[] cloudBlobStreamBinderTypes)
        {
            _cloudBlobStreamBinderTypes = cloudBlobStreamBinderTypes;
        }

        public void Initialize(ExtensionConfigContext context)
        {
            foreach (var type in _cloudBlobStreamBinderTypes)
            {
                var instance = Activator.CreateInstance(type);

                var bindingType = GetBindingValueType(type);
                var method = this.GetType().GetMethod("AddAdapter", BindingFlags.Static | BindingFlags.NonPublic);
                method = method.MakeGenericMethod(bindingType);
                method.Invoke(null, new object[] { context, instance });
            }
        }

        static void AddAdapter<T>(ExtensionConfigContext context, ICloudBlobStreamBinder<T> x)
        {
            context.AddConverter<Stream, T>(stream => x.ReadFromStreamAsync(stream, CancellationToken.None).Result);

            context.AddConverter<ApplyConversion<T, Stream>, object>(pair =>
            {
                T value = pair.Value;
                Stream stream = pair.Existing;
                x.WriteToStreamAsync(value, stream, CancellationToken.None).Wait();
                return null;
            });
        }
                
        internal static Type GetBindingValueType(Type binderType)
        {
            Type binderInterfaceType = GetCloudBlobStreamBinderInterface(binderType);
            return binderInterfaceType.GetGenericArguments()[0];
        }

        private static Type GetCloudBlobStreamBinderInterface(Type binderType)
        {
            return binderType.GetInterfaces().First(
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICloudBlobStreamBinder<>));
        }
    }
}
