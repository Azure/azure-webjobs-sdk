// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class AttributeClonerTests
    {
        // Basic attribute. 1 property. No ctor.
        public class Attr1 : Attribute
        {
            [AutoResolve]
            public string Path { get; set; }
        }

        // 2 attributes, set in ctor. And a non-resolved attribute.
        public class Attr2 : Attribute
        {
            public Attr2(string resolvedProp2, string constantProp)
            {
                ResolvedProp2 = resolvedProp2;
                ConstantProp = constantProp;
            }

            [AutoResolve]
            public string ResolvedProp1 { get; set; }

            [AutoResolve]
            public string ResolvedProp2 { get; private set; }

            public string ConstantProp { get; private set; }

            [AppSetting]
            public string ResolvedSetting { get; set; }

            [AppSetting(Default = "default")]
            public string DefaultSetting { get; set; }

            // This lookup will be under ConnectionStrings
            [ConnectionString(Default = "default")]
            public string Connection { get; set; }
        }

        public class Attr3 : Attribute
        {
            [AppSetting]
            public string Required { get; set; }

            [AppSetting(Default = "default")]
            public string Default { get; set; }
        }

        public class Attr4 : Attribute
        {
            [AppSetting]
            public string AppSetting { get; set; }

            [AutoResolve]
            public string AutoResolve { get; set; }
        }

        // Test with DefaultValue.MemberName
        public class Attr5 : Attribute
        {
            [AutoResolve(Default = "{sys.MethodName}")]
            public string AutoResolve { get; set; }
        }


        // Test with DefaultValue.MemberName
        public class BadDefaultAttr : Attribute
        {
            // Default can't access instance binding data (x). 
            [AutoResolve(Default = "{sys.MethodName}-{x}")]
            public string AutoResolve { get; set; }
        }

        public class InvalidAnnotation : Attribute
        {
            // only one of appsetting/autoresolve allowed
            [AppSetting]
            [AutoResolve]
            public string Required { get; set; }
        }

        public class InvalidNonStringAutoResolve : Attribute
        {
            // AutoResolve must be string 
            [AutoResolve]
            public bool Required { get; set; }
        }

        public class AttributeWithResolutionPolicy : Attribute
        {
            [AutoResolve(ResolutionPolicyType = typeof(TestResolutionPolicy))]
            public string PropWithPolicy { get; set; }

            [AutoResolve]
            public string PropWithoutPolicy { get; set; }

            [AutoResolve(ResolutionPolicyType = typeof(AutoResolveAttribute))]
            public string PropWithInvalidPolicy { get; set; }

            [AutoResolve(ResolutionPolicyType = typeof(NoDefaultConstructorResolutionPolicy))]
            public string PropWithConstructorlessPolicy { get; set; }

            internal string ResolutionData { get; set; }
        }

        public class TestResolutionPolicy : IResolutionPolicy
        {
            public string TemplateBind(PropertyInfo propInfo, Attribute attribute, BindingTemplate template, IReadOnlyDictionary<string, object> bindingData)
            {
                // set some internal state for the binding rules to use later
                ((AttributeWithResolutionPolicy)attribute).ResolutionData = "value1";

                return template.Bind(bindingData);
            }
        }

        public class NoDefaultConstructorResolutionPolicy : IResolutionPolicy
        {
            public NoDefaultConstructorResolutionPolicy(string someValue)
            {
            }

            public string TemplateBind(PropertyInfo propInfo, Attribute attribute, BindingTemplate template, IReadOnlyDictionary<string, object> bindingData)
            {
                throw new NotImplementedException();
            }
        }

        // Validation with AutoResolve
        [Binding]
        public class ValidationWithAutoResolveAttribute : Attribute
        {
            [RegularExpression("a+")]
            [AutoResolve]
            public string Value { get; set; }
        }

        // Validation with AppSetting
        [Binding]
        public class ValidationWithAppSettingAttribute : Attribute
        {
            [RegularExpression("a+")]
            [AppSetting]
            public string Value { get; set; }
        }

        // Validation with neither AppSetting nor AutoResolve
        [Binding]
        public class ValidationOnlyAttribute : Attribute
        {
            // Allow  { } that look like token substitution, but it's not since this isn't AutoResolve
            [RegularExpression("^.{1,3}$")]
            public string Value { get; set; }
        }

        // Helper to easily generate a fixed binding contract.
        private static IReadOnlyDictionary<string, Type> GetBindingContract(params string[] names)
        {
            var d = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                d[name] = typeof(string);
            }
            return d;
        }

        private static IReadOnlyDictionary<string, Type> GetBindingContract(Dictionary<string, object> values)
        {
            var d = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in values)
            {
                d[kv.Key] = typeof(string);
            }
            return d;
        }

        private static readonly IReadOnlyDictionary<string, Type> emptyContract = new Dictionary<string, Type>();
        private static readonly IConfiguration _emptyConfig = new ConfigurationBuilder().Build();

        // Enforce binding contracts statically.
        [Fact]
        public void BindingContractMismatch()
        {
            Attr1 a1 = new Attr1 { Path = "{name}" };

            try
            {
                var cloner = new AttributeCloner<Attr1>(a1, emptyContract, _emptyConfig);
                Assert.True(false, "Should have caught binding contract mismatch");
            }
            catch (InvalidOperationException e)
            {
                string expectedError = "Unable to resolve binding parameter 'name'. Binding expressions must map to either a value provided by the trigger or a property of the value the trigger is bound to, or must be a system binding expression (e.g. sys.randguid, sys.utcnow, etc.).";
                Assert.Equal(expectedError, e.Message);
            }
        }


        // Test on an attribute that does NOT implement IAttributeInvokeDescriptor
        // Key parameter is a property (not ctor)
        [Fact]
        public void InvokeString()
        {
            Attr1 a1 = new Attr1 { Path = "%test%" };

            Assert.Null(a1 as IAttributeInvokeDescriptor<Attr1>); // Does not implement the interface

            var nameResolver = new FakeNameResolver();
            nameResolver._dict["test"] = "ABC";

            var cloner = new AttributeCloner<Attr1>(a1, emptyContract, _emptyConfig, nameResolver);
            Attr1 attr2 = cloner.ResolveFromInvokeString("xy");

            Assert.Equal("xy", attr2.Path);
        }

        // Test on an attribute that does NOT implement IAttributeInvokeDescriptor
        // Multiple resolved properties.
        [Fact]
        public void InvokeStringMultipleResolvedProperties()
        {
            Attr2 attr = new Attr2("{p2}", "constant")
            {
                ResolvedProp1 = "{p1}"
            };

            var cloner = new AttributeCloner<Attr2>(attr, GetBindingContract("p1", "p2"), _emptyConfig);

            Attr2 attrResolved = cloner.ResolveFromBindings(new Dictionary<string, object> { { "p1", "v1" }, { "p2", "v2" } });

            Assert.Equal("v1", attrResolved.ResolvedProp1);
            Assert.Equal("v2", attrResolved.ResolvedProp2);
            Assert.Equal(attr.ConstantProp, attrResolved.ConstantProp);

            var invokeString = cloner.GetInvokeString(attrResolved);
            var attr2 = cloner.ResolveFromInvokeString(invokeString);

            Assert.Equal(attrResolved.ResolvedProp1, attr2.ResolvedProp1);
            Assert.Equal(attrResolved.ResolvedProp2, attr2.ResolvedProp2);
            Assert.Equal(attrResolved.ConstantProp, attr2.ConstantProp);
        }

        // Easy case - default ctor and all settable properties.
        [Fact]
        public void NameResolver()
        {
            Attr1 a1 = new Attr1 { Path = "x%appsetting%y-{k}" };

            var nameResolver = new FakeNameResolver().Add("appsetting", "ABC");
            var cloner = new AttributeCloner<Attr1>(a1, GetBindingContract("k"), _emptyConfig, nameResolver);

            // Get the attribute with %% resolved (happens at indexing time), but not {} (not resolved until runtime)
            var attrPre = cloner.GetNameResolvedAttribute();
            Assert.Equal("xABCy-{k}", attrPre.Path);

            Dictionary<string, object> values = new Dictionary<string, object>()
            {
                { "k", "v" }
            };
            var ctx = GetCtx(values);
            var attr2 = cloner.ResolveFromBindingData(ctx);

            Assert.Equal("xABCy-v", attr2.Path);
        }

        // Easy case - default ctor and all settable properties.
        [Fact]
        public void Easy()
        {
            Attr1 a1 = new Attr1 { Path = "{request.headers.authorization}-{key2}" };

            Dictionary<string, object> values = new Dictionary<string, object>()
            {
                { "request", new {
                        headers = new {
                            authorization = "ey123"
                        }
                    }
                },
                { "key2", "val2" }
            };
            var ctx = GetCtx(values);

            var cloner = new AttributeCloner<Attr1>(a1, GetBindingContract("request", "key2"), _emptyConfig);
            var attr2 = cloner.ResolveFromBindingData(ctx);

            Assert.Equal("ey123-val2", attr2.Path);
        }

        [Fact]
        public void Setting()
        {
            Attr2 a2 = new Attr2(string.Empty, string.Empty) { ResolvedSetting = "appsetting" };

            var nameResolver = new FakeNameResolver();

            var config = TestHelpers.CreateInMemoryCollection()
                .AddSetting("appsetting", "ABC")
                .AddSetting("connectionStrings:default", "fromConnStr")
                .BuildConfiguration();

            var cloner = new AttributeCloner<Attr2>(a2, emptyContract, config, nameResolver);

            var a2Cloned = cloner.GetNameResolvedAttribute();
            Assert.Equal("ABC", a2Cloned.ResolvedSetting);
            Assert.Equal("fromConnStr", a2Cloned.Connection);
        }

        [Fact]
        public void Setting_WithNoValueInResolver_ThrowsIfNoDefault()
        {
            Attr2 a2 = new Attr2(string.Empty, string.Empty) { ResolvedSetting = "appsetting" };
            var exc = Assert.Throws<InvalidOperationException>(() => new AttributeCloner<Attr2>(a2, emptyContract, _emptyConfig));
            Assert.Equal($"Unable to resolve the value for property 'Attr2.ResolvedSetting'. Make sure the setting exists and has a valid value.", exc.Message);
        }

        [Fact]
        public void Setting_WithNoValueInResolver_UsesDefault()
        {
            Attr2 a2 = new Attr2(string.Empty, string.Empty) { ResolvedSetting = "appsetting" };
            var nameResolver = new FakeNameResolver();
            var config = TestHelpers.CreateInMemoryCollection()
                .AddSetting("appsetting", "ABC")
                .AddSetting("default", "default")
                .BuildConfiguration();

            var cloner = new AttributeCloner<Attr2>(a2, emptyContract, config, nameResolver);

            var a2Cloned = cloner.GetNameResolvedAttribute();
            Assert.Equal("default", a2Cloned.DefaultSetting);
        }

        [Fact]
        public void AppSettingAttribute_Resolves_IfDefaultSet()
        {
            Attr3 a3 = new Attr3() { Required = "req", Default = "env" };
            var nameResolver = new FakeNameResolver();

            var config = TestHelpers.CreateInMemoryCollection()
                .AddSetting("env", "envval")
                .AddSetting("req", "reqval")
                .BuildConfiguration();

            var cloner = new AttributeCloner<Attr3>(a3, emptyContract, config, nameResolver);
            var cloned = cloner.GetNameResolvedAttribute();
            Assert.Equal("reqval", cloned.Required);
            Assert.Equal("envval", cloned.Default);
        }

        [Fact]
        public void AppSettingAttribute_Resolves_IfDefaultMatched()
        {
            Attr3 a3 = new Attr3() { Required = "req" };
            var nameResolver = new FakeNameResolver();

            var config = TestHelpers.CreateInMemoryCollection()
                .AddSetting("default", "defaultval")
                .AddSetting("req", "reqval")
                .BuildConfiguration();

            var cloner = new AttributeCloner<Attr3>(a3, emptyContract, config, nameResolver);
            var cloned = cloner.GetNameResolvedAttribute();
            Assert.Equal("reqval", cloned.Required);
            Assert.Equal("defaultval", cloned.Default);
        }

        [Fact]
        public void AppSettingAttribute_Throws_IfDefaultUnmatched()
        {
            Attr3 a3 = new Attr3() { Required = "req" };
            Assert.Throws<InvalidOperationException>(() => new AttributeCloner<Attr3>(a3, emptyContract, _emptyConfig));
        }

        [Fact]
        public void Setting_Null()
        {
            Attr2 a2 = new Attr2(string.Empty, string.Empty);

            var cloner = new AttributeCloner<Attr2>(a2, emptyContract, _emptyConfig);

            Attr2 a2Clone = cloner.ResolveFromBindingData(GetCtx(null));

            Assert.Null(a2Clone.ResolvedSetting);
        }

        [Fact]
        public void AppSettingAttribute_ResolvesWholeValueAsSetting()
        {
            Attr4 a4 = new Attr4();
            var name = "test{x}and%y%";
            a4.AppSetting = a4.AutoResolve = name;

            var nameResolver = new FakeNameResolver()
                .Add("y", "Setting");

            var config = TestHelpers.CreateInMemoryCollection()
                .AddSetting(name, "AppSetting")
                .BuildConfiguration();

            var cloner = new AttributeCloner<Attr4>(a4, GetBindingContract("x"), config, nameResolver);
            var cloned = cloner.GetNameResolvedAttribute();
            // autoresolve resolves tokens
            Assert.Equal("test{x}andSetting", cloned.AutoResolve);
            // appsetting treats entire string as app setting name
            Assert.Equal("AppSetting", cloned.AppSetting);
        }

        [Fact]
        public void AppSettingAttribute_DoesNotThrowIfNullValueAndNoDefault()
        {
            Attr4 a4 = new Attr4();
            a4.AutoResolve = "auto";
            a4.AppSetting = null;

            var nameResolver = new FakeNameResolver();
            var cloner = new AttributeCloner<Attr4>(a4, emptyContract, _emptyConfig, nameResolver);
            var cloned = cloner.GetNameResolvedAttribute();
            Assert.Equal("auto", cloned.AutoResolve);
            Assert.Equal(null, cloned.AppSetting);
        }

        public class ConnectionStrings : Attribute
        {
            [ConnectionString]
            public string Connection { get; set; }

            [ConnectionString]
            public string ConnectionAtRoot { get; set; }

            [ConnectionString]
            public string ConnectionWithNestedPath { get; set; }

            [ConnectionString(Default = "defaultSetting")]
            public string ConnectionOverrideDefault { get; set; }

            [ConnectionString(Default = "defaultSetting")]
            public string ConnectionWithDefault { get; set; }

            [ConnectionString(Default = "nested:path:defaultKey")]
            public string ConnectionWithNestedDefaultPath { get; set; }

            [AppSetting(Default = "defaultSetting")]
            public string AppSettingWithDefault { get; set; }
        }

        [Fact]
        public void ConnectionStringAttribute_ResolvesCorrectly()
        {
            ConnectionStrings attr = new ConnectionStrings
            {
                Connection = "key",
                ConnectionAtRoot = "rootKey",
                ConnectionWithNestedPath = "nested:path:key",
                ConnectionOverrideDefault = "overridden"
            };

            var config = TestHelpers.CreateInMemoryCollection()
                .AddSetting("key", "unused")
                .AddSetting("rootKey", "rootValue")
                .AddSetting("connectionStrings:key", "connectionStringsValue")
                .AddSetting("nested:path:key", "nestedPathValue")
                .AddSetting("nested:path:defaultKey", "nestedPathDefaultValue")
                .AddSetting("connectionStrings:overridden", "connectionStringsOverriddenValue")
                .AddSetting("connectionStrings:defaultSetting", "fromConnStr")
                .AddSetting("defaultSetting", "fromRoot")
                .BuildConfiguration();

            var nameResolver = new FakeNameResolver();
            var cloner = new AttributeCloner<ConnectionStrings>(attr, emptyContract, config, nameResolver);
            var cloned = cloner.GetNameResolvedAttribute();

            Assert.Equal("connectionStringsValue", cloned.Connection);
            Assert.Equal("rootValue", cloned.ConnectionAtRoot);
            Assert.Equal("nestedPathValue", cloned.ConnectionWithNestedPath);
            Assert.Equal("connectionStringsOverriddenValue", cloned.ConnectionOverrideDefault);
            Assert.Equal("nestedPathDefaultValue", cloned.ConnectionWithNestedDefaultPath);
            Assert.Equal("fromConnStr", cloned.ConnectionWithDefault);
            Assert.Equal("fromRoot", cloned.AppSettingWithDefault);
        }

        [Fact]
        public void AttributeCloner_Throws_IfAppSettingAndAutoResolve()
        {
            InvalidAnnotation a = new InvalidAnnotation();
            var exc = Assert.Throws<InvalidOperationException>(() => new AttributeCloner<InvalidAnnotation>(a, emptyContract, _emptyConfig));
            Assert.Equal("Property 'Required' can only be annotated with one of the types AppSettingAttribute, AutoResolveAttribute, and ConnectionStringAttribute.", exc.Message);
        }

        [Fact]
        public void AttributeCloner_Throws_IfAutoResolveIsNotString()
        {
            var a = new InvalidNonStringAutoResolve();
            var exc = Assert.Throws<InvalidOperationException>(() => new AttributeCloner<InvalidNonStringAutoResolve>(a, emptyContract, _emptyConfig));
            Assert.Equal("ConnectionStringAttribute, AutoResolveAttribute, or AppSettingAttribute property 'Required' must be of type string.", exc.Message);
        }

        // Default to MethodName  kicks in if the (pre-resolved) value is null. 
        [Theory]
        [InlineData(null, "MyMethod")]
        [InlineData("", "MyMethod")]
        [InlineData("   ", "MyMethod")] // whitespace
        [InlineData("{empty}", "")]
        [InlineData("{value}", "123")]
        [InlineData("%empty2%", "")]
        [InlineData("%value2%", "456")]
        [InlineData("foo-{sys.MethodName}", "foo-MyMethod")]
        public void DefaultMethodName(string propValue, string expectedValue)
        {
            Attr5 attr = new Attr5 { AutoResolve = propValue }; // Pick method name

            var nameResolver = new FakeNameResolver()
               .Add("empty2", "")
               .Add("value2", "456");

            Dictionary<string, object> values = new Dictionary<string, object>()
            {
                { "empty", "" },
                { "value", "123" }
            };
            new SystemBindingData
            {
                MethodName = "MyMethod"
            }.AddToBindingData(values);

            var ctx = GetCtx(values);

            var cloner = new AttributeCloner<Attr5>(attr, GetBindingContract(values), _emptyConfig, nameResolver);

            var attr2 = cloner.ResolveFromBindingData(ctx);
            Assert.Equal(expectedValue, attr2.AutoResolve);
        }

        // Default can't access instance binding data. 
        [Fact]
        public void DefaultCantAccessInstanceData()
        {
            var attr = new BadDefaultAttr(); // use default value, which is bad. 

            Dictionary<string, object> values = new Dictionary<string, object>()
            {
                { "x", "123" },
                { SystemBindingData.Name, new SystemBindingData
                {
                     MethodName = "MyMethod"
                } }
            };
            var ctx = GetCtx(values);

            try
            {
                new AttributeCloner<BadDefaultAttr>(attr, GetBindingContract(values), _emptyConfig);
                Assert.False(true);
            }
            catch (InvalidOperationException e)
            {
                // Verify message. 
                Assert.True(e.Message.StartsWith("Default contract can only refer to the 'sys' binding data: "));
            }
        }

        // Malformed %% fail in ctor.
        // It's important that the failure comes from the attr cloner ctor because that means it
        // will occur during indexing time (ie, sooner than runtime).
        [Fact]
        public void Fail_MalformedPath_MutableProperty()
        {
            Attr1 attr = new Attr1 { Path = "%bad" };

            Assert.Throws<InvalidOperationException>(() => new AttributeCloner<Attr1>(attr, emptyContract, _emptyConfig));
        }

        [Fact]
        public void Fail_MalformedPath_CtorArg()
        {
            var attr = new Attr2("%bad", "constant");

            Assert.Throws<InvalidOperationException>(() => new AttributeCloner<Attr2>(attr, emptyContract, _emptyConfig));
        }

        // Malformed %% fail in ctor.
        // It's important that the failure comes from the attr cloner ctor because that means it
        // will occur during indexing time (ie, sooner than runtime).
        [Fact]
        public void Fail_MissingPath()
        {
            Attr1 attr = new Attr1 { Path = "%missing%" };

            Assert.Throws<InvalidOperationException>(() => new AttributeCloner<Attr1>(attr, emptyContract, _emptyConfig));
        }

        private static readonly Action<object> skipValidation = (_) => { };

        [Fact]
        public void TryAutoResolveValue_UnresolvedValue_ThrowsExpectedException()
        {
            var resolver = new FakeNameResolver();
            var attribute = new Attr2(string.Empty, string.Empty)
            {
                ResolvedSetting = "MySetting"
            };
            var prop = attribute.GetType().GetProperty("ResolvedSetting");
            var attr = prop.GetCustomAttribute<AppSettingAttribute>();
            string resolvedValue = "MySetting";

            var ex = Assert.Throws<InvalidOperationException>(() => AttributeCloner<Attr2>.GetConfigurationResolver(resolvedValue, attr.Default, prop, skipValidation, s => _emptyConfig[s]));
            Assert.Contains("Unable to resolve the value for property 'Attr2.ResolvedSetting'.", ex.Message);
        }

        [Fact]
        public void GetPolicy_ReturnsDefault_WhenNoSpecifiedPolicy()
        {
            PropertyInfo propInfo = typeof(AttributeWithResolutionPolicy).GetProperty(nameof(AttributeWithResolutionPolicy.PropWithoutPolicy));
            AutoResolveAttribute attr = propInfo.GetCustomAttribute<AutoResolveAttribute>();
            IResolutionPolicy policy = AttributeCloner<AttributeWithResolutionPolicy>.GetPolicy(attr.ResolutionPolicyType, propInfo);

            Assert.IsType<DefaultResolutionPolicy>(policy);
        }

        [Fact]
        public void GetPolicy_Returns_SpecifiedPolicy()
        {
            PropertyInfo propInfo = typeof(AttributeWithResolutionPolicy).GetProperty(nameof(AttributeWithResolutionPolicy.PropWithPolicy));

            AutoResolveAttribute attr = propInfo.GetCustomAttribute<AutoResolveAttribute>();
            IResolutionPolicy policy = AttributeCloner<AttributeWithResolutionPolicy>.GetPolicy(attr.ResolutionPolicyType, propInfo);

            Assert.IsType<TestResolutionPolicy>(policy);
        }

        [Fact]
        public void GetPolicy_Throws_IfPolicyDoesNotImplementInterface()
        {
            PropertyInfo propInfo = typeof(AttributeWithResolutionPolicy).GetProperty(nameof(AttributeWithResolutionPolicy.PropWithInvalidPolicy));
            AutoResolveAttribute attr = propInfo.GetCustomAttribute<AutoResolveAttribute>();
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => AttributeCloner<AttributeWithResolutionPolicy>.GetPolicy(attr.ResolutionPolicyType, propInfo));

            Assert.Equal($"The {nameof(AutoResolveAttribute.ResolutionPolicyType)} on {nameof(AttributeWithResolutionPolicy.PropWithInvalidPolicy)} must derive from {typeof(IResolutionPolicy).Name}.", ex.Message);
        }

        [Fact]
        public void GetPolicy_Throws_IfPolicyHasNoDefaultConstructor()
        {
            PropertyInfo propInfo = typeof(AttributeWithResolutionPolicy).GetProperty(nameof(AttributeWithResolutionPolicy.PropWithConstructorlessPolicy));
            AutoResolveAttribute attr = propInfo.GetCustomAttribute<AutoResolveAttribute>();
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => AttributeCloner<AttributeWithResolutionPolicy>.GetPolicy(attr.ResolutionPolicyType, propInfo));

            Assert.Equal($"The {nameof(AutoResolveAttribute.ResolutionPolicyType)} on {nameof(AttributeWithResolutionPolicy.PropWithConstructorlessPolicy)} must derive from {typeof(IResolutionPolicy).Name} and have a default constructor.", ex.Message);
        }


        // If there are { } tokens, don't validate until after substitution (runtime) 
        [Fact]
        public void Validation_Late()
        {
            // with { } , can't determine if it's valid until after resolution. 
            ValidationWithAutoResolveAttribute attr = new ValidationWithAutoResolveAttribute { Value = "a{name}" };

            // Can't fail yet. 
            var cloner = new AttributeCloner<ValidationWithAutoResolveAttribute>(attr, GetBindingContract("name"), _emptyConfig);

            // Valid 
            {
                Dictionary<string, object> values = new Dictionary<string, object>()
                {
                    { "name", "aa" },  // Ok 
                };
                var ctx = GetCtx(values);

                var attr2 = cloner.ResolveFromBindingData(ctx);
                Assert.Equal("aaa", attr2.Value);
            }

            // Invalid 
            {
                Dictionary<string, object> values = new Dictionary<string, object>()
                {
                    { "name", "b" },  // regex failure 
                };
                var ctx = GetCtx(values);

                Assert.Throws<InvalidOperationException>(() =>
                       cloner.ResolveFromBindingData(ctx));
            }
        }

        // If there are no { }, we can determine validity immediately. 
        [Fact]
        public void Validation_Early_Succeed()
        {
            ValidationWithAutoResolveAttribute attr = new ValidationWithAutoResolveAttribute { Value = "aaa" };

            Dictionary<string, object> values = new Dictionary<string, object>()
            {
                { "name", "green" },
            };
            var ctx = GetCtx(values);

            var cloner = new AttributeCloner<ValidationWithAutoResolveAttribute>(attr, GetBindingContract("name"), _emptyConfig);
            var attr2 = cloner.ResolveFromBindingData(ctx);

            Assert.Equal("aaa", attr2.Value);
        }

        // If there are no { }, we can determine validity immediately. Fail upfront. 
        [Fact]
        public void Validation_Early_Fail()
        {
            const string IllegalValue = "green";

            // No { }, so we can determine validity immediately 
            ValidationWithAutoResolveAttribute attr = new ValidationWithAutoResolveAttribute { Value = IllegalValue };

            Dictionary<string, object> values = new Dictionary<string, object>()
            {
                { "name", "ignored" }, // ignored since no {name} token in the attr
            };
            var ctx = GetCtx(values);

            try
            {
                new AttributeCloner<ValidationWithAutoResolveAttribute>(attr, GetBindingContract("name"), _emptyConfig);
                Assert.False(true, "Validation should have failed");
            }
            catch (InvalidOperationException e)
            {
                // Since this is [AutoResolve], include the illegal value in the message.
                Assert.True(e.Message.Contains(IllegalValue));
            }
        }

        // With Validation + AppSetting         
        [Fact]
        public void Validation_With_AppSetting_Early_Fail()
        {
            const string IllegalValue = "bbbb";

            // No { }, so we can determine validity immediately 
            ValidationWithAppSettingAttribute attr = new ValidationWithAppSettingAttribute { Value = "bbb" };

            Dictionary<string, object> values = new Dictionary<string, object>()
            {
                { "name", "ignored" }, // ignored since no {name} token in the attr
            };
            var ctx = GetCtx(values);

            try
            {
                new AttributeCloner<ValidationWithAppSettingAttribute>(attr, GetBindingContract("name"), _emptyConfig);
                Assert.False(true, "Validation should have failed");
            }
            catch (InvalidOperationException e)
            {
                // Since this is [AppSetting], don't include the illegal value in the message. It could be secret. 
                Assert.False(e.Message.Contains(IllegalValue));
            }
        }

        // No AppSetting/AutoResolve, so validate early 
        [Theory]
        [InlineData("{x}", true)]
        [InlineData("%x%", true)]
        [InlineData("{x", true)]
        [InlineData("illegal", false)]
        public void Validation_Direct_Early_Fail(string value, bool shouldSucceed)
        {
            ValidationOnlyAttribute attr = new ValidationOnlyAttribute { Value = value };

            Dictionary<string, object> values = new Dictionary<string, object>()
            {
                { "x", "ignored" }, // ignored since not autoresolve
            };
            var ctx = GetCtx(values);

            try
            {
                var cloner = new AttributeCloner<ValidationOnlyAttribute>(attr, GetBindingContract("name"), _emptyConfig);

                if (shouldSucceed)
                {
                    // Success
                    var attrResolved = cloner.ResolveFromBindings(values);

                    // no autoresolve/appsetting, so the final value should be the same as the input value. 
                    Assert.Equal(value, attrResolved.Value);

                    return;
                }
                Assert.False(true, "Validation should have failed");
            }
            catch (InvalidOperationException e)
            {
                Assert.False(shouldSucceed);

                // Non-appsetting, so include the value in the message
                Assert.True(e.Message.Contains(value));
            }
        }

        private static BindingContext GetCtx(IReadOnlyDictionary<string, object> values)
        {
            BindingContext ctx = new BindingContext(
                new ValueBindingContext(null, CancellationToken.None),
                values);
            return ctx;
        }
    }
}