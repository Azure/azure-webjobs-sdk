// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class TestIndexCollector : IFunctionIndexCollector
    {
        public List<FunctionDescriptor> Functions = new List<FunctionDescriptor>();

        public void Add(IFunctionDefinition function, FunctionDescriptor descriptor, MethodInfo method)
        {
            Functions.Add(descriptor);
        }
    }
}
