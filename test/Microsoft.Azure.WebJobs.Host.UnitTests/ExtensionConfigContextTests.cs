// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class ExtensionConfigContextTests
    {
        private static readonly IConfiguration _config = new ConfigurationBuilder().Build();

        [Fact]
        public void BasicRules()
        {
            ConverterManager cm = new ConverterManager();
            INameResolver nr = new FakeNameResolver();
            var ctx = new ExtensionConfigContext(_config, nr, cm, null, null);

            // Simulates extension initialization scope.
            {
                var binding = ctx.AddBindingRule<TestAttribute>();

                binding.WhenIsNull("Mode").
                        BindToInput<int>(attr => attr.Value);

                binding.WhenIsNotNull("Mode").
                        BindToInput<string>(attr => attr.ToString());

                StringWriter sw = new StringWriter();
                binding.DebugDumpGraph(sw);

                AssertStringEqual(
@"[TestAttribute] -->[filter: (Mode == null)]-->Int32
[TestAttribute] -->[filter: (Mode != null)]-->String", sw.ToString());
            }
        }

        static void AssertStringEqual(string expected, string actual)
        {
            var a = expected.Trim().Replace("\r", "");
            var b = actual.Trim().Replace("\r", "");

            Assert.Equal(a, b);
        }

        // Test register converters via the ExtensionConfigContext interfaces.
        [Fact]
        public void Converters()
        {
            ConverterManager cm = new ConverterManager();
            var ctx = new ExtensionConfigContext(_config, null, cm, null, null);

            // Simulates extension initialization scope.
            {
                ctx.AddBindingRule<TestAttribute>().AddConverter<int, string>(val => "specific"); // specific 
                ctx.AddConverter<int, string>(val => "general"); // general 
            }
            ctx.ApplyRules();

            {
                var generalConverter = cm.GetSyncConverter<int, string, Attribute>();
                var result = generalConverter(12, null, null);
                Assert.Equal("general", result);
            }

            {
                var specificConverter = cm.GetSyncConverter<int, string, TestAttribute>();
                var result = specificConverter(12, null, null);
                Assert.Equal("specific", result);
            }
        }

        [Fact]
        public void Error_IfMissingBindingAttribute()
        {
            var ctx = new ExtensionConfigContext(_config, null, null, null, null);

            // 'Attribute' 
            Assert.Throws<InvalidOperationException>(() => ctx.AddBindingRule<Attribute>());
        }

        [Fact]
        public void CallingAddBindingRule_Multiple_Times()
        {
            var ctx = new ExtensionConfigContext(_config, null, null, null, null);

            // First time is fine
            var rule1 = ctx.AddBindingRule<TestAttribute>();

            // Second time on the same Attribute gives the same instance
            var rule2 = ctx.AddBindingRule<TestAttribute>();

            Assert.Same(rule1, rule2);
        }

        [Fact]
        public void ErrorOnDanglingWhen()
        {
            var ctx = new ExtensionConfigContext(_config, null, null, null, null);

            // Simulates extension initialization scope.
            {
                var binding = ctx.AddBindingRule<TestAttribute>();
                binding.WhenIsNull("Mode"); // Error! Dangling filter, should end in a Bind() call.
            }
            Assert.Throws<InvalidOperationException>(() => ctx.ApplyRules());
        }

        [Binding]
        public class TestAttribute : Attribute
        {
            public string Mode { get; set; }
            public int Value { get; set; }
        }
    }
}
