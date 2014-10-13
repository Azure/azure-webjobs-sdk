﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class ClassPropertyAccessorFactory<TReflected> : IPropertyAccessorFactory<TReflected>
        where TReflected : class
    {
        private static readonly ClassPropertyAccessorFactory<TReflected> _instance =
            new ClassPropertyAccessorFactory<TReflected>();

        private ClassPropertyAccessorFactory()
        {
        }

        public static ClassPropertyAccessorFactory<TReflected> Instance
        {
            get { return _instance; }
        }

        public IPropertyGetter<TReflected, TProperty> CreateGetter<TProperty>(PropertyInfo property)
        {
            return ClassPropertyGetter<TReflected, TProperty>.Create(property);
        }

        public IPropertySetter<TReflected, TProperty> CreateSetter<TProperty>(PropertyInfo property)
        {
            return ClassPropertySetter<TReflected, TProperty>.Create(property);
        }
    }
}
