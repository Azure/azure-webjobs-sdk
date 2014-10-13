﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Converters;

namespace Microsoft.Azure.WebJobs.Host.Tables
{
    internal class ConverterPropertySetter<TReflected, TProperty, TConvertedProperty>
        : IPropertySetter<TReflected, TConvertedProperty>
    {
        private readonly IConverter<TConvertedProperty, TProperty> _converter;
        private readonly IPropertySetter<TReflected, TProperty> _propertySetter;

        public ConverterPropertySetter(IConverter<TConvertedProperty, TProperty> converter,
            IPropertySetter<TReflected, TProperty> propertySetter)
        {
            if (converter == null)
            {
                throw new ArgumentNullException("converter");
            }
            else if (propertySetter == null)
            {
                throw new ArgumentNullException("propertySetter");
            }

            _converter = converter;
            _propertySetter = propertySetter;
        }

        public void SetValue(ref TReflected instance, TConvertedProperty value)
        {
            TProperty propertyValue = _converter.Convert(value);
            _propertySetter.SetValue(ref instance, propertyValue);
        }
    }
}
