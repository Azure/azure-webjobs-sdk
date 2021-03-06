﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Xunit;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using System.IO;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class ConverterManagerTests
    {
        static ValueBindingContext context = null;

        // Can always convert a type to itself. 
        [Fact]
        public void Identity()
        {
            var cm = new ConverterManager(); // empty 

            var identity = cm.GetSyncConverter<string, string, Attribute>();

            var value = "abc";
            var x1 = identity(value, null, context);
            Assert.Same(x1, value);
        }

        // Explicit converters take precedence. 
        [Fact]
        public void ExactMatchOverride()
        {
            var cm = new ConverterManager(); // empty 
            cm.AddConverter<string, string>(x => "*" + x + "*");

            var func = cm.GetSyncConverter<string, string, Attribute>();

            var x1 = func("x", null, context);
            Assert.Equal("*x*", x1);
        }

        private T TestDefaultConverter<F, T>(F from, T to, ConverterManager cm = null, Action<T> assertion = null)
        {
            if (cm == null) {
                cm = new ConverterManager();
            }
            var converter = cm.GetSyncConverter<F, T, Attribute>();
            Assert.NotNull(converter);
            var result = converter(from, null, null);
            if (assertion == null)
            {
                Assert.Equal(to, result);
            } else
            {
                assertion(result);
            }
            return result;
        }

        [Fact]
        public void ByteToString_DefaultConverter()
        {
            var s = "ab";
            TestDefaultConverter(Encoding.UTF8.GetBytes(s), s);
        }

        [Fact]
        public void IEnumerableToJArray_DefaultConverter()
        {
            var obj = JObject.Parse("{ \"a\": 2 }");
            IEnumerable<JObject> enumerable = new List<JObject>() { obj, obj };
            var jarray = new JArray(obj, obj);
            TestDefaultConverter(enumerable, jarray);
        }

        [Fact]
        public void ObjectToJArray_ChainConverter()
        {
            var jobjString = "{ \"a\": 2 }";
            var obj = JObject.Parse(jobjString);
            var cm = new ConverterManager();
            cm.AddConverter<string, IEnumerable<JObject>, Attribute>((str, attr) => new List<JObject>() { JObject.Parse(str), JObject.Parse(str) });
            var jarray = new JArray(obj, obj);
            TestDefaultConverter(jobjString, jarray, cm);
        }

        // Use a value binding context to stamp causality on a JObject        
        // This is what [Queue] does. 
        [Fact]
        public void UseValueBindingContext()
        {
            var cm = new ConverterManager(); // empty 

            Guid instance = Guid.NewGuid();
            var testContext = new ValueBindingContext(new FunctionBindingContext(instance, CancellationToken.None), CancellationToken.None);

            FuncAsyncConverter converter = 
            (object obj, Attribute attr, ValueBindingContext ctx) => {
                Assert.Same(ctx, testContext);
                var result = JObject.FromObject(obj);
                result["$"] = ctx.FunctionInstanceId;
                return Task.FromResult<object>(result);
            };
            cm.AddConverter<object, JObject, Attribute>(converter);
            cm.AddConverter<JObject, Wrapper>(str => new Wrapper { Value = str.ToString() });

            // Expected: 
            //    Other --> JObject,  
            //    JObject --> string ,  (builtin) 
            //    string --> Wrapper
            var func = cm.GetSyncConverter<Other, Wrapper, Attribute>();

            var value = new Other { Value2 = "abc" };
            Wrapper x1 = func(value, null, testContext);
            // strip whitespace
            string val = Regex.Replace(x1.Value, @"\s", "");
            string expected = String.Format("{{\"Value2\":\"abc\",\"$\":\"{0}\"}}", instance);

            Assert.Equal(expected, val);
    }

        // Explicit converters take precedence. 
        [Fact]
        public void Inheritence()
        {
            var cm = new ConverterManager(); // empty             
            var func = cm.GetSyncConverter<DerivedWrapper, Wrapper, Attribute>();

            var obj = new DerivedWrapper { Value = "x" };
            Wrapper x1 = func(obj, null, context);
            Assert.Same(x1, obj);
        }

        // Object is a catch-all
        [Fact]
        public void CatchAll()
        {
            var cm = new ConverterManager(); // empty 
            cm.AddConverter<object, Wrapper>(x => new Wrapper { Value = x.ToString() });

            var func = cm.GetSyncConverter<int, Wrapper, Attribute>();

            var x1 = func(123, null, context);
            Assert.Equal("123", x1.Value);
        }

        // Byte[] and String converters. 
        [Fact]
        public void StringAndByteArray()
        {
            var cm = new ConverterManager(); // empty             

            // No default byte[]-->Wrapper conversion. 
            var fromBytes = cm.GetSyncConverter<byte[], Wrapper, Attribute>();
            Assert.Null(fromBytes);

            // Add a string-->Wrapper conversion
            cm.AddConverter<string, Wrapper>(str => new Wrapper { Value = str });

            var fromString = cm.GetSyncConverter<string, Wrapper, Attribute>();
            Wrapper obj1 = fromString("abc", null, context);
            Assert.Equal("abc", obj1.Value);

            // Now we can get a byte-->string  , composed from a default (byte[]-->string) + supplied (string-->Wrapper)
            byte[] bytes = Encoding.UTF8.GetBytes("abc");

            fromBytes = cm.GetSyncConverter<byte[], Wrapper, Attribute>();
            Assert.NotNull(fromBytes);
            Wrapper obj2 = fromBytes(bytes, null, context);
            Assert.Equal("abc", obj2.Value);

            // Now override the default. Uppercase the string so we know it used our custom converter.
            cm.AddConverter<byte[], string>(b => Encoding.UTF8.GetString(b).ToUpper());
            fromBytes = cm.GetSyncConverter<byte[], Wrapper, Attribute>();
            Wrapper obj3 = fromBytes(bytes, null, context);
            Assert.Equal("ABC", obj3.Value);
        }

        // BinaryData converters. 
        [Fact]
        public void BinaryData()
        {
            byte[] data = new byte[124];
            new Random().NextBytes(data);
            TestDefaultConverter(data, new BinaryData(data), assertion: result => Assert.Equal(data, result.ToArray()));

            TestDefaultConverter(new BinaryData(data), data);
        }

        // Overload conversions on type if they're using different attributes. 
        [Fact]
        public void AttributeOverloads()
        {
            var cm = new ConverterManager(); // empty 
            cm.AddConverter<Wrapper, string, TestAttribute>((x, attr) => string.Format("[t1:{0}-{1}]", x.Value, attr.Flag));
            cm.AddConverter<Wrapper, string, TestAttribute2>((x, attr) => string.Format("[t2:{0}-{1}]", x.Value, attr.Flag));

            // Since converter was registered for a specific attribute, it must be queried by that attribute. 
            var funcMiss = cm.GetSyncConverter<Wrapper, string, Attribute>();
            Assert.Null(funcMiss);

            // Each attribute type has its own conversion function
            var func1 = cm.GetSyncConverter<Wrapper, string, TestAttribute>();
            Assert.NotNull(func1);
            var x1 = func1(new Wrapper { Value = "x" }, new TestAttribute("y"), context);
            Assert.Equal("[t1:x-y]", x1);

            var func2 = cm.GetSyncConverter<Wrapper, string, TestAttribute2>();
            Assert.NotNull(func2);
            var x2 = func2(new Wrapper { Value = "x" }, new TestAttribute2("y"), context);
            Assert.Equal("[t2:x-y]", x2);
        }

        // Explicit converters take precedence. 
        [Fact]
        public void AttributeOverloads2()
        {
            var cm = new ConverterManager(); // empty 
            cm.AddConverter<Wrapper, string, TestAttribute>((x, attr) => string.Format("[t1:{0}-{1}]", x.Value, attr.Flag));
            cm.AddConverter<Wrapper, string>(x => string.Format("[common:{0}]", x.Value));
                        
            // This has an exact match on attribute and gives the specific function we registered.
            var func1 = cm.GetSyncConverter<Wrapper, string, TestAttribute>();
            Assert.NotNull(func1);
            var x1 = func1(new Wrapper { Value = "x" }, new TestAttribute("y"), context);
            Assert.Equal("[t1:x-y]", x1);

            // Nothing registered for this attribute, so we return the converter that didn't require any attribute.
            var func2 = cm.GetSyncConverter<Wrapper, string, TestAttribute2>();
            Assert.NotNull(func2);
            var x2 = func2(new Wrapper { Value = "x" }, new TestAttribute2("y"), context);
            Assert.Equal("[common:x]", x2);
        }


        // Test converter using Open generic types
        class TypeConverterWithTwoGenericArgs<TInput, TOutput> :
            IConverter<TInput, TOutput>
        {
            public TypeConverterWithTwoGenericArgs(ConverterManagerTests config)
            { 
                // We know this is only used by a single test invoking with this combination of params.
                Assert.Equal(typeof(String), typeof(TInput));
                Assert.Equal(typeof(int), typeof(TOutput));

                config._counter++;
            }

            public TOutput Convert(TInput input)
            {
                var str = (string)(object)input;
                return (TOutput)(object)int.Parse(str);
            }
        }

        [Fact]
        public void OpenTypeConverterWithTwoGenericArgs()
        {
            Assert.Equal(0, _counter);
            var cm = new ConverterManager();

            // Register a converter builder. 
            // Builder runs once; converter runs each time.
            // Uses open type to match. 
            cm.AddConverter<TypeWrapperIsString, int, Attribute>(typeof(TypeConverterWithTwoGenericArgs<,>), this);

            var converter = cm.GetSyncConverter<string, int, Attribute>();

            Assert.Equal(12, converter("12", new TestAttribute(null), null));
            Assert.Equal(34, converter("34", new TestAttribute(null), null));

            Assert.Equal(1, _counter); // converterBuilder is only called once. 

            // 'char' as src parameter doesn't match the type predicate. 
            Assert.Null(cm.GetSyncConverter<char, int, Attribute>());
        }

        // Verify that all open Types within the same context must resolve to the same type. 
        // This is important when resolving (Source,Dest) pairs. 
        [Fact]
        public void OpenTypeContext()
        {
            var ctx = new OpenTypeMatchContext();

            var ot = OpenType.FromType(typeof(OpenType));
            var otArray = OpenType.FromType(typeof(OpenType[]));

            Assert.True(ot.IsMatch(typeof(string), ctx));
            Assert.False(ot.IsMatch(typeof(string[]), ctx));
            Assert.True(otArray.IsMatch(typeof(string[]), ctx));
            Assert.True(ot.IsMatch(typeof(DateTime))); // We could match DateTime normally
            Assert.False(ot.IsMatch(typeof(DateTime), ctx)); // false because we already commited to 'string' within this context
        }

        // Test converter using Open generic types, rearranging generics
        class TypeConverterWithOneGenericArg<TElement> : 
            IConverter<TElement, IEnumerable<TElement>>
        {
            public IEnumerable<TElement> Convert(TElement input)
            {
                // Trivial rule. 
                return new TElement[] { input, input, input };
            }
        }

        [Fact]
        public void OpenTypeConverterWithOneGenericArg()
        {
            var cm = new ConverterManager();

            // Register a converter builder. 
            // Builder runs once; converter runs each time.
            // Uses open type to match. 
            // Also test the IEnumerable<OpenType> pattern. 

            cm.AddConverter<OpenType, IEnumerable<OpenType>, Attribute>(typeof(TypeConverterWithOneGenericArg<>));

            var attr = new TestAttribute(null);
            {
                // Doesn't match since the OpenTypes would resolve to different Ts
                var converter = cm.GetSyncConverter<object, IEnumerable<int>, Attribute>();
                Assert.Null(converter);
            }
            {
                var converter = cm.GetSyncConverter<int, IEnumerable<int>, Attribute>();
                Assert.Equal(new int[] { 1, 1, 1 }, converter(1, attr, null));
            }

            {
                var converter = cm.GetSyncConverter<string, IEnumerable<string>, Attribute>();
                Assert.Equal(new string[] { "a", "a", "a" }, converter("a", attr, null));
            }
        }

        // Replace
        [Fact]
        public void Replace()
        {
            var cm = new ConverterManager(); // empty

            cm.AddConverter<string, int>(str => int.Parse(str));

            var c = cm.GetSyncConverter<string, int, Attribute>();
            Assert.Equal(123, c("123", null, null));

            // Replace the original 
            cm.AddConverter<string, int>(str => int.Parse(str)* 10);

            Assert.Equal(123, c("123", null, null)); // Original converter still works as is. 
            var c2 = cm.GetSyncConverter<string, int, Attribute>();
            Assert.Equal(1230, c2("123", null, null)); // New converter pulls replaced results. 
        }

        // Precedence: an exact match wins over an OpenType match 
        [Fact]
        public void Precedence()
        {
            var cm = new ConverterManager(); // empty

            cm.AddConverter<OpenType, int, Attribute>((srcType, destType) =>
            {
                return (src, attr, ctx) => Task.FromResult<object>(int.Parse(src.ToString()) * 100);
            });
            cm.AddConverter<string, int>(str => int.Parse(str)); // Exact types
            

            var c = cm.GetSyncConverter<string, int, Attribute>();
            Assert.Equal(123, c("123", null, null)); // Exact takes precedence

            var c2 = cm.GetSyncConverter<double, int, Attribute>();
            Assert.Equal(9900, c2(99, null, null)); // Uses the open converter
        }

        // String-->T converter is not enough to support JObject serialization. 
        [Fact]
        public void String2TDoesNotEnableJObject()
        {
            var cm = new ConverterManager(); // empty

            cm.AddConverter<string, Wrapper>(str => new Wrapper { Value = str });

            var objSrc = new Other { Value2 = "abc" };

            // Json Serialize: (Other --> string)
            // custom          (string -->Wrapper)
            var func = cm.GetSyncConverter<Other, Wrapper, Attribute>();
            Assert.Null(func);
        }

        // If A --> Jobject, and JObject -->B, then we can do A --> B via a JObject. 
        [Fact]
        public void JObjectMiddleman()
        {
            var cm = new ConverterManager();

            cm.AddConverter<OpenType.Poco, JObject, Attribute>( (src, dest) =>
                (input, attr2, ctx) =>
                {
                    var val = JObject.FromObject(input);
                    val["c"] = "custom"; // stamp an extra field to verify it's our serialization 
                    return Task.FromResult<object>(val);
                });
           
            cm.AddConverter<JObject, Other>(obj => new Other { Value2 = obj["c"].ToString() });
            var attr = new TestAttribute(null);

            // Non poco types don't match 
            Assert.Null(cm.GetSyncConverter<int, JObject, Attribute>());
            Assert.Null(cm.GetSyncConverter<Object, JObject, Attribute>());
                        
            var converter = cm.GetSyncConverter<Wrapper, JObject, Attribute>();
            Assert.NotNull(converter);

            // Wrapper --> JObject --> Other
            var c2 = cm.GetSyncConverter<Wrapper, Other, Attribute>(); 
            Assert.NotNull(c2);

            Other result = c2(new Wrapper { Value = "x" }, null, null);
            Assert.Equal("custom", result.Value2);


            // If we now add a direct converter, that takes precedence 
            cm.AddConverter<Wrapper, Other>(input => new Other { Value2 = input.Value });
            var direct = cm.GetSyncConverter<Wrapper, Other, Attribute>();

            Other result2 = direct(new Wrapper { Value = "x" }, null, null);
            Assert.Equal("x", result2.Value2);
        }


        class OpenArrayConverter<T>
            : IConverter<T[], string>
        {
            public string Convert(T[] input)
            {
                return string.Join(",", input);
            }
        }

        // Test OpenType[] --> converter
        [Fact]
        public void OpenTypeArray()
        {
            var cm = new ConverterManager();
                        
            cm.AddConverter<OpenType[], string, Attribute>(typeof(OpenArrayConverter<>));
            var attr = new TestAttribute(null);

            var converter = cm.GetSyncConverter<int[], string, Attribute>();
            Assert.Equal("1,2,3", converter(new int[] { 1, 2, 3 }, attr, null));
        }

        // Test concrete array converters. 
        [Fact]
        public void ClosedTypeArray()
        {
            var cm = new ConverterManager();

            cm.AddConverter<int[], string, Attribute>(new OpenArrayConverter<int>());
            var attr = new TestAttribute(null);

            var converter = cm.GetSyncConverter<int[], string, Attribute>();
            Assert.Equal("1,2,3", converter(new int[] { 1, 2, 3 }, attr, null));
        }

        // Counter used by tests to verify that converter ctors are only run once and then shared across 
        // multiple invocations. 
        private int _counter;

        class ConverterInstanceMethod :
            IConverter<string, int>
        {
            // Converter discovered for OpenType4 test. Used directly. 
            public int Convert(string input)
            {
                return int.Parse(input);
            }
        }

        [Fact]
        public void OpenTypeSimpleConcreteConverter()
        {
            Assert.Equal(0, _counter);
            var cm = new ConverterManager();

            // Register a converter builder. 
            // Builder runs once; converter runs each time.
            // Uses open type to match. 
            cm.AddConverter<TypeWrapperIsString, int, Attribute>(new ConverterInstanceMethod());

            var converter = cm.GetSyncConverter<string, int, Attribute>();

            Assert.Equal(12, converter("12", new TestAttribute(null), null));
            Assert.Equal(34, converter("34", new TestAttribute(null), null));

            Assert.Equal(0, _counter); // passed in instantiated object; counter never incremented. 

            // 'char' as src parameter doesn't match the type predicate. 
            Assert.Null(cm.GetSyncConverter<char, int, Attribute>());
        }

        // Test converter using concrete types. 
        class TypeConverterWithConcreteTypes
            : IConverter<string, int>
        {
            public TypeConverterWithConcreteTypes(ConverterManagerTests config)
            {
                config._counter++;
            }

            public int Convert(string input)
            {
                return int.Parse(input);
            }
        }

        [Fact]
        public void OpenTypeConverterWithConcreteTypes()
        {
            Assert.Equal(0, _counter);
            var cm = new ConverterManager();

            // Register a converter builder. 
            // Builder runs once; converter runs each time.
            // Uses open type to match. 
            cm.AddConverter<TypeWrapperIsString, int, Attribute>(typeof(TypeConverterWithConcreteTypes), this);

            var converter = cm.GetSyncConverter<string, int, Attribute>();

            Assert.Equal(12, converter("12", new TestAttribute(null), null));
            Assert.Equal(34, converter("34", new TestAttribute(null), null));

            Assert.Equal(1, _counter); // converterBuilder is only called once. 

            // 'char' as src parameter doesn't match the type predicate. 
            Assert.Null(cm.GetSyncConverter<char, int, Attribute>());
        }

        [Fact]
        public void OpenTypeTest()
        {
            int count = 0;
            var cm = new ConverterManager();

            // Register a converter builder. 
            // Builder runs once; converter runs each time.
            // Uses open type to match. 
            cm.AddConverter<TypeWrapperIsString, int, Attribute>(
                (typeSrc, typeDest) =>
                {
                    count++;
                    Assert.Equal(typeof(String), typeSrc);
                    Assert.Equal(typeof(int), typeDest);

                    FuncAsyncConverter converter2 = (input, attr, ctx) =>
                    {
                        string s = (string)input;
                        return Task.FromResult<object>(int.Parse(s));
                    };
                    return converter2;
                });

            var converter = cm.GetSyncConverter<string, int, Attribute>();
            Assert.NotNull(converter);
            Assert.Equal(12, converter("12", new TestAttribute(null), null));
            Assert.Equal(34, converter("34", new TestAttribute(null), null));            

            Assert.Equal(1, count); // converterBuilder is only called once. 

            // 'char' as src parameter doesn't match the type predicate. 
            Assert.Null(cm.GetSyncConverter<char, int, Attribute>());
        }


        // Test with async converter 
        public class UseAsyncConverter : IAsyncConverter<int, string>
        {
            public Task<string> ConvertAsync(int i, CancellationToken cancellationToken)
            {
                return Task.FromResult(i.ToString());
            }
        }

        // Test non-generic Task<string>, use instance match. 
        [Fact]
        public void UseUseAsyncConverterTest()
        {
            var cm = new ConverterManager();

            cm.AddConverter<int, string, Attribute>(new UseAsyncConverter());

            var converter = cm.GetSyncConverter<int, string, Attribute>();

            Assert.Equal("12", converter(12, new TestAttribute(null), null));            
        }

        // Test with async converter 
        public class UseGenericAsyncConverter<T> :
            IAsyncConverter<int, T>
        {
            public Task<T> ConvertAsync(int i, CancellationToken token)
            {
                Assert.Equal(typeof(string), typeof(T));
                return Task.FromResult((T) (object) i.ToString());
            }
        }

        // Test generic Task<T>, use typing match. 
        [Fact]
        public void UseGenericAsyncConverterTest()
        {
            var cm = new ConverterManager();

            cm.AddConverter<int, string, Attribute>(typeof(UseGenericAsyncConverter<>));

            var converter = cm.GetSyncConverter<int, string, Attribute>();

            Assert.Equal("12", converter(12, new TestAttribute(null), null));
        }

        // Sample types to excercise pattern matcher
        public class Foo<T1, T2> : IConverter<T1, IDictionary<char, T2>>
        {
            public IDictionary<char, T2> Convert(T1 input)
            {
                throw new NotImplementedException();
            }

            public T1[] _genericArray;
        }

        // Unit tests for TestPatternMatcher.ResolveGenerics
        [Fact]
        public void TestPatternMatcher_ResolveGenerics()
        {
            var typeFoo = typeof(Foo<,>);
            var int1 = typeFoo.GetInterfaces()[0]; // IConverter<T1, IDictionary<T1, T2>>
            var typeFoo_T1 = typeFoo.GetGenericArguments()[0];
            var typeGenericArray = typeFoo.GetField("_genericArray").FieldType;
            var typeIConverter_T1 = int1.GetGenericArguments()[0];
            var typeIConverter_IDictChar_T2 = int1.GetGenericArguments()[1];

            var genArgs = new Dictionary<string, Type>
            {
                { "T1", typeof(int) },
                { "T2", typeof(string) }
            };

            Assert.Equal(typeof(int), PatternMatcher.ResolveGenerics(typeof(int), genArgs));

            Assert.Equal(typeof(int[]), PatternMatcher.ResolveGenerics(typeGenericArray, genArgs));
            
            var typeFooIntStr = typeof(Foo<int, string>);
            Assert.Equal(typeFooIntStr, PatternMatcher.ResolveGenerics(typeFooIntStr, genArgs));

            Assert.Equal(typeof(int), PatternMatcher.ResolveGenerics(typeFoo_T1, genArgs));
            Assert.Equal(typeof(int), PatternMatcher.ResolveGenerics(typeIConverter_T1, genArgs));
            Assert.Equal(typeof(IDictionary<char, string>), PatternMatcher.ResolveGenerics(typeIConverter_IDictChar_T2, genArgs));

            Assert.Equal(typeof(Foo<int, string>), PatternMatcher.ResolveGenerics(typeFoo, genArgs));

            Assert.Equal(typeof(IConverter<int, IDictionary<char, string>>), 
                PatternMatcher.ResolveGenerics(int1, genArgs));
        }

        public class TestConverter :
            IConverter<Attribute, IAsyncCollector<string>>, // binding rule converter
            IConverter<string, byte[]> // general type converter
        {
            public IAsyncCollector<string> Convert(Attribute input)
            {
                return null;
            }

            byte[] IConverter<string, byte[]>.Convert(string input)
            {
                return null;
            }
        }

        [Fact]
        public void PatternMatcher_Succeeds_WhenBindingRuleConverterExists()
        {
            var pm = PatternMatcher.New(typeof(TestConverter));
            var generalConverter = pm.TryGetConverterFunc(typeof(string), typeof(byte[]));
            Assert.NotNull(generalConverter);

            var bindingRuleConverter = pm.TryGetConverterFunc(typeof(Attribute), typeof(IAsyncCollector<string>));
            Assert.NotNull(bindingRuleConverter);
        }

        private class TestConverterFakeEntity
            : IConverter<JObject, IFakeEntity>
        {
            public IFakeEntity Convert(JObject obj)
            {
                return new MyFakeEntity { Property = obj["Property1"].ToString() };
            }
        }

        private class TestConverterFakeEntity<T>
            : IConverter<T, IFakeEntity>
        {
            public IFakeEntity Convert(T item)
            {
                Assert.IsType<PocoEntity>(item); // test only calls this with PocoEntity 
                var d = (PocoEntity) (object) item;
                string propValue = d.Property2;
                return new MyFakeEntity { Property = propValue };
            }
        }

        // Test sort of rules that we have in tables. 
        // Rules can overlap, so make sure that the right rule is dispatched. 
        // Poco is a base class of  Jobject and IFakeEntity.
        // Give each rule its own unique converter and ensure each converter is called.  
        [Fact]
        public void TestConvertFakeEntity()
        {
            var cm = new ConverterManager();

            // Derived<ITableEntity> --> IFakeEntity  [automatic] 
            // JObject --> IFakeEntity
            // Poco --> IFakeEntity
            cm.AddConverter<JObject, IFakeEntity, Attribute>(typeof(TestConverterFakeEntity));
            cm.AddConverter<OpenType, IFakeEntity, Attribute>(typeof(TestConverterFakeEntity<>));

            {
                var converter = cm.GetSyncConverter<IFakeEntity, IFakeEntity, Attribute>();
                var src = new MyFakeEntity { Property = "123" };
                var dest = converter(src, null, null);
                Assert.Same(src, dest); // should be exact same instance - no conversion 
            }

            {
                var converter = cm.GetSyncConverter<JObject, IFakeEntity, Attribute>();
                JObject obj = new JObject();
                obj["Property1"] = "456";
                var dest = converter(obj, null, null);
                Assert.Equal("456", dest.Property);
            }

            {
                var converter = cm.GetSyncConverter<PocoEntity, IFakeEntity, Attribute>();
                var src = new PocoEntity { Property2 = "789" };                
                var dest = converter(src, null, null);
                Assert.Equal("789", dest.Property);
            }
        }

        // Class that implements IFakeEntity. Test conversions.  
        class MyFakeEntity : IFakeEntity
        {
            public string Property { get; set; }
        }

        interface IFakeEntity
        {
            string Property { get; }
        }

        // Poco class that can be converted to IFakeEntity, but doesn't actually implement IFakeEntity. 
        class PocoEntity
        {
            // Give a different property name so that we can enforce the exact converter.
            public string Property2 { get; set; }
        }

        class TypeWrapperIsString : OpenType
        {
            // Predicate is invoked by ConverterManager to determine if a type matches. 
            public override bool IsMatch(Type t, OpenTypeMatchContext context)
            {
                return t == typeof(string);
            }
        }

        // Custom type
        public class Wrapper
        {
            public string Value;
        }

        public class DerivedWrapper : Wrapper
        {
            public int Other;
        }

        // Another custom type, with no relation to Wrapper
        public class Other
        {
            public string Value2;
        }

        public class TestAttribute : Attribute
        {
            public TestAttribute(string flag)
            {
                this.Flag = flag;
            }
            public string Flag { get; set; }
        }

        // Different attribute
        public class TestAttribute2 : Attribute
        {
            public TestAttribute2(string flag)
            {
                this.Flag = flag;
            }
            public string Flag { get; set; }
        }
    }    
}