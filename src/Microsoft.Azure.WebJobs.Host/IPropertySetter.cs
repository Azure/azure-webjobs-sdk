﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host
{
    internal interface IPropertySetter<TReflected, TProperty>
    {
        void SetValue(ref TReflected instance, TProperty value);
    }
}
