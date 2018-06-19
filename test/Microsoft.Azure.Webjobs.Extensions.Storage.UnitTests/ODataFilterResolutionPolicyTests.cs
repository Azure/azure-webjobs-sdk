// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Microsoft.Azure.WebJobs.Description;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
#if false // $$$ AttributeCloner  is internal 
    public class ODataFilterResolutionPolicyTests
    {
        public class AttributeWithResolutionPolicy : Attribute
        {
            [AutoResolve(ResolutionPolicyType = typeof(ODataFilterResolutionPolicy))]
            public string PropWithMarkerPolicy { get; set; }

           internal string ResolutionData { get; set; }
        }

        [Fact]
        public void GetPolicy_ReturnsODataFilterPolicy_ForMarkerType()
        {
            // This is a special-case marker type to handle TableAttribute.Filter. We cannot directly list ODataFilterResolutionPolicy
            // because BindingTemplate doesn't exist in the core assembly.
            PropertyInfo propInfo = typeof(AttributeWithResolutionPolicy).GetProperty(nameof(AttributeWithResolutionPolicy.PropWithMarkerPolicy));

            AutoResolveAttribute attr = propInfo.GetCustomAttribute<AutoResolveAttribute>();
            IResolutionPolicy policy = AttributeCloner<AttributeWithResolutionPolicy>.GetPolicy(attr.ResolutionPolicyType, propInfo);

            Assert.IsType<Host.Bindings.ODataFilterResolutionPolicy>(policy);
        }
    }
#endif
}
