// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Microsoft.Azure.WebJobs.Description;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Common
{
    // Test for binding validator 
    public class BindingProviderFilterTests
    {
        [Binding]
        public class TestAttribute : Attribute
        {
            public TestAttribute()
            {

            }
            public TestAttribute(string path)
            {
                this.Path = path;
            }

            [AutoResolve]
            public string Path {get;set;}
        }

        class Program
        {
            public string _value;
            public void Func([Test("%x%")] string x)
            {
                _value = x;
            }

            public void FuncNull([Test] string x)
            {
                _value = x;
            }
        }

        // Fitler that throws a validation error. 
        [Fact]
        public void TestValidationError()
        {
            var nr = new FakeNameResolver().Add("x", "error");
            var host = TestHelpers.NewJobHost<Program>(nr, new FakeExtClient());

            TestHelpers.AssertIndexingError(() => host.Call("Func"), "Program.Func", FakeExtClient.IndexErrorMsg);
        }
              
        // Filter takes the not-null branch
        [Fact]
        public void TestSuccessNotNull()
        {
            var prog = new Program();
            var jobActivator = new FakeActivator();
            jobActivator.Add(prog);

            var nr = new FakeNameResolver().Add("x", "something"); 
            var host = TestHelpers.NewJobHost<Program>(nr, jobActivator, new FakeExtClient());
            host.Call(nameof(Program.Func));

            // Skipped first rule, applied second 
            Assert.Equal(prog._value, "something");
        }

        // Filter takes the Null branch
        [Fact]
        public void TestSuccessNull()
        {
            var prog = new Program();
            var jobActivator = new FakeActivator();
            jobActivator.Add(prog);

            var nr = new FakeNameResolver().Add("x", "something");
            var host = TestHelpers.NewJobHost<Program>(nr, jobActivator, new FakeExtClient());
            host.Call(nameof(Program.FuncNull));

            // Skipped first rule, applied second 
            Assert.Equal(prog._value, "xxx");
        }

        public class FakeExtClient : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                // Add [Test] support
                var x = context.AddBindingRule<TestAttribute>();
                x.WhenIsNotNull("Path").BindToInput<string>(attr => attr.Path).AddValidator(Validate);
                x.WhenIsNull("Path").BindToInput<string>(attr => "xxx");
            }

            // Validate the post-resolved attribute. 
            private void Validate(TestAttribute attribute, Type arg2)
            {
                if (attribute.Path == "error")
                {
                    throw new InvalidOperationException(IndexErrorMsg);
                }
            }

            public const string IndexErrorMsg = "error 12345";
        }
    }
}