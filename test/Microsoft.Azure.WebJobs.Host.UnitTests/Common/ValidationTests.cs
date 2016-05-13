// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Common
{
    public class ValidationTests
    {

        public class TestAttribute : Attribute
        {
            public bool Bad { get; set; }

            internal const string ErrorMessage = "Test attribute is not valid!";

            [AutoResolve]
            public string Path { get; set; }
        }

        // Some arbitrary type. 
        public class Widget { } 

        public class FunctionBase
        {
            [NoAutomaticTrigger]
            public void Valid() { }
        }

        public class BadFunction : FunctionBase
        {
            public void Bad([Test(Bad = true, Path = "%k1%-{k2}")] Widget x)
            {
            }        
        }

        public class GoodFunction : FunctionBase
        {
            public void Good([Test(Bad= false, Path = "%k1%-{k2}")] Widget x)
            {
            }
        }

        public class FakeExtClient : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var bf = context.Config.BindingFactory;

                // Add [Test] support
                var rule = bf.BindToExactType<TestAttribute, Widget>(attr => new Widget());
                context.RegisterBindingRules<TestAttribute>(ValidateAtIndexTime, rule);
            }

            public void ValidateAtIndexTime(TestAttribute attribute, Type parameterType)
            {
                // Verify that FX is passing the expected paramteer type.
                Assert.Equal(typeof(Widget), parameterType);

                // Test that validation is valled after INameResolver has handled %%, but before {} are resolved. 
                Assert.Equal("v1-{k2}", attribute.Path);

                if (attribute.Bad)
                {
                    throw new InvalidOperationException(TestAttribute.ErrorMessage);
                }
            }
        }

        [Fact]
        public void TestValidatorFails()
        {
            var nr = new FakeNameResolver().Add("k1", "v1");
            var host = TestHelpers.NewJobHost<BadFunction>(new FakeExtClient(), nr);

            try
            {
                host.Call("Valid"); // Should get indexing error from 'Bad' function 
            }
            catch (FunctionIndexingException e)
            {
                Assert.Equal("Error indexing method 'BadFunction.Bad'", e.Message);
                Assert.Equal(TestAttribute.ErrorMessage, e.InnerException.Message);
                return;
            }
            Assert.True(false, "Invoker should have failed");
        }

        [Fact]
        public void TestValidatorSucceeds()
        {
            var nr = new FakeNameResolver().Add("k1", "v1");
            var host = TestHelpers.NewJobHost<GoodFunction>(new FakeExtClient(), nr);
            host.Call("Good", new { k2 = "xxxx" } ); 
        }
    }
}
