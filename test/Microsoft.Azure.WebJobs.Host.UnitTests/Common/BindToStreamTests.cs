// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Common
{
    // Test BindingFactory's BindToInput rule.
    // Provide some basic types, converters, builders and make it very easy to test a
    // variety of configuration permutations. 
    // Each Client configuration is its own test case. 
    public class BindToStreamTests
    {
        // Each of the TestConfigs below implement this. 
        interface ITest<TConfig>
        {
            void Test(TestJobHost<TConfig> host);
        }

        [Binding]
        public class TestStreamAttribute : Attribute
        {
            [AutoResolve]
            public string Path { get; set; }

            public FileAccess? Access { get; set; }

            public TestStreamAttribute()
            {
            }

            // Constructor layout like Blob.
            // Can't assign a Nullable<T> in an attribute parameter list. Must be in ctor. 
            public TestStreamAttribute(string path)
            {
                this.Path = path;
            }

            public TestStreamAttribute(string path, FileAccess access)
                : this(path)
            {
                this.Access = access;
            }
        }

        // Test that leaving an out-parameter as null does not create a stream.
        // This means it shouldn't even call the Attribute-->Stream converter function. 
        [Fact]
        public void NullOutParamDoesNotWriteStream()
        {
            TestWorker<ConfigNullOutParam>();
        }

        public class ConfigNullOutParam : IExtensionConfigProvider, ITest<ConfigNullOutParam>,
            IConverter<TestStreamAttribute, Stream>
        {
            public void Initialize(ExtensionConfigContext context)
            {
                context.AddBindingRule<TestStreamAttribute>().
                    BindToStream(this, FileAccess.ReadWrite);
            }

            public void Test(TestJobHost<ConfigNullOutParam> host)
            {
                host.Call("WriteString");
                // Convert was never called 
            }

            public Stream Convert(TestStreamAttribute input)
            {
                // Should never even try to create a stream when 'out T' is set to null.
                throw new InvalidOperationException("Test cases should never create stream");
            }

            public void WriteString(
                [TestStream] out string x
                )
            {
                x = null; // Don't write anything 
            }
        }

        // Verify that BindToStream rule still honors [AutoResolve] on the attribute properties.
        [Fact]
        public void TestAutoResolve()
        {
            TestWorker<ConfigAutoResolve>();
        }

        public class ConfigAutoResolve : IExtensionConfigProvider, ITest<ConfigAutoResolve>,
            IConverter<TestStreamAttribute, Stream>
        {
            private string _log;

            public void Initialize(ExtensionConfigContext context)
            {
                context.AddBindingRule<TestStreamAttribute>().
                    BindToStream(this, FileAccess.ReadWrite);
            }

            public void Test(TestJobHost<ConfigAutoResolve> host)
            {
                host.Call("Read", new { x = 456 });
                // Convert was never called 

                Assert.Equal("456-123", _log);
            }

            public Stream Convert(TestStreamAttribute input)
            {
                if (input.Access == FileAccess.Read)
                {
                    var value = input.Path; // Will exercise the [AutoResolve]
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(value));
                    stream.Position = 0;
                    return stream;
                }
                throw new InvalidOperationException();
            }

            public void Read(
                [TestStream(Path="{x}-%y%")] string value
                )
            {
                _log = value;
            }
        }

        // Verify that BindToStream rule still honors [AutoResolve] on the attribute properties.
        [Fact]
        public void TestCustom()
        {
            TestWorker<ConfigAutoResolve>();
        }

        public class ConfigCustom : IExtensionConfigProvider, ITest<ConfigCustom>,
            IConverter<TestStreamAttribute, Stream>
        {
            private string _log;
            private MemoryStream _writeStream;

            const string ReadTag = "xx";

            public void Initialize(ExtensionConfigContext context)
            {
                context.AddBindingRule<TestStreamAttribute>().
                    BindToStream(this, FileAccess.ReadWrite);

                // Override the Stream --> String converter
                context.AddConverter<Stream, string>(stream => ReadTag); 

                context.AddConverter<ApplyConversion<string, Stream>, object> ((pair) =>
                 {
                     var val = pair.Value;
                     var stream = pair.Existing;
                     using (var sr = new StreamWriter(stream))
                     {
                         sr.Write("yy"); // custom
                         sr.Write(val);
                     }
                     return null;
                 });
            }

            public void Test(TestJobHost<ConfigCustom> host)
            {
                host.Call("Read");
                Assert.Equal(_log, ReadTag);

                host.Call("Write");

                var content = _writeStream.ToArray(); // safe to call even after Dispose()
                var str = Encoding.UTF8.GetString(content);
                Assert.Equal("yya", str);
            }

            public Stream Convert(TestStreamAttribute input)
            {
                if (input.Access == FileAccess.Read)
                {
                    var value = input.Path; // Will exercise the [AutoResolve]
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(value));
                    stream.Position = 0;
                    return stream;
                }
                if (input.Access == FileAccess.Write)
                {
                    var stream = new MemoryStream();
                    _writeStream = stream;
                    return stream;
                }
                throw new InvalidOperationException();
            }

            public void Read([TestStream] string value)
            {
                _log = value;
            }
            public void Read([TestStream] out string value)
            {
                value = "a";
            }
        }

        // If the file does not exist, we should return null from the converter. 
        // The Framework *can't* assume that an exception translates to NotExist, since there's
        // no standard exception, so the file might exist but throw a permission denied exception. 
        [Fact]
        public void TestNotExist()
        {
            TestWorker<ConfigNotExist>();
        }

        public class ConfigNotExist : IExtensionConfigProvider, ITest<ConfigNotExist>,
            IConverter<TestStreamAttribute, Stream>
        {
            private object _log;

            public void Initialize(ExtensionConfigContext context)
            {
                context.AddBindingRule<TestStreamAttribute>().
                    BindToStream(this, FileAccess.ReadWrite);
            }

            public void Test(TestJobHost<ConfigNotExist> host)
            {
                host.Call("Read1");
                Assert.Null(_log);

                host.Call("Read2");
                Assert.Null(_log);

                host.Call("Read3");
                Assert.Null(_log);

                host.Call("Read4");
                Assert.Null(_log);
            }

            public Stream Convert(TestStreamAttribute input)
            {
                if (input.Access == FileAccess.Read)
                {
                    return null; // Simulate not-exist 
                }
                throw new InvalidOperationException();
            }

            public void Read1([TestStream("path", FileAccess.Read)] Stream value)
            {
                _log = value;
            }

            public void Read2([TestStream] TextReader value)
            {
                _log = value;
            }

            public void Read3([TestStream] string value)
            {
                _log = value;
            }

            public void Read4([TestStream] byte[] value)
            {
                _log = value;
            }
        }


        // Bulk test the success case for different parameter types we can bind to. 
        [Fact]
        public void TestStream()
        {
            TestWorker<ConfigStream>();
        }

        public class ConfigStream : IExtensionConfigProvider, ITest<ConfigStream>,
            IAsyncConverter<TestStreamAttribute, Stream>
        {
            // Set by test functions; verified 
            string _log;
            MemoryStream _writeStream;

            public void Initialize(ExtensionConfigContext context)
            {
                context.AddBindingRule<TestStreamAttribute>().
                    BindToStream(this, FileAccess.ReadWrite);
            }

            public void Test(TestJobHost<ConfigStream> host)
            {
                foreach (var funcName in new string[]
                {
                    "StreamRead", "StringRead", "ByteArrayRead", "TextReaderRead"
                })
                {
                    _log = null;
                    host.Call(funcName, new { k = 1 });
                    Assert.Equal("Hello", _log);
                }

                // Test writes. Verify the stream content. 
                foreach (var funcName in new string[]
               {
                    "WriteStream",
                    "WriteStream2",
                   "WriteTextWriter1",
                   "WriteTextWriter2",
                   "WriteTextWriter3",
                   "WriteString",
                   "WriteByteArray"
                })
                {
                    _writeStream = null;
                    host.Call(funcName, new { k = funcName });

                    var content = _writeStream.ToArray(); // safe to call even after Dispose()
                    var str = Encoding.UTF8.GetString(content);

                    // The comparison will also verify there is no BOM written.
                    Assert.Equal(_writeMessage, str);
                }
            }

            #region Read overloads

            public void StreamRead(
                [TestStream("path", FileAccess.Read)] Stream sr
                )
            {
                List<byte> lb = new List<byte>();
                while (true)
                {
                    var b = sr.ReadByte();
                    if (b == -1)
                    {
                        break;
                    }
                    lb.Add((byte)b);
                }
                ByteArrayRead(lb.ToArray());
            }

            // Read as string 
            public void StringRead(
                [TestStream] String str
                )
            {
                _log = str;
            }

            // Read as byte[] 
            public void ByteArrayRead(
                [TestStream] byte[] bytes
                )
            {
                _log = Encoding.UTF8.GetString(bytes);
            }

            public void TextReaderRead(
                [TestStream("path", FileAccess.Read)] TextReader tr
                )
            {
                _log = tr.ReadToEnd();
            }
            #endregion // Read Overloads

            #region Write overloads 
            const string _writeMessage = "HelloFromWriter";

            public void WriteStream(
                [TestStream("path", FileAccess.Write)] Stream tw
                )
            {
                var bytes = Encoding.UTF8.GetBytes(_writeMessage);
                tw.Write(bytes, 0, bytes.Length);
                // Framework will flush and close the stream. 
            }

            public void WriteStream2(
                [TestStream("path", FileAccess.Write)] Stream stream
                )
            {
                var bytes = Encoding.UTF8.GetBytes(_writeMessage);
                stream.Write(bytes, 0, bytes.Length);
                
                stream.Close();  // Ok if user code explicitly closes. 
            }

            // Explicit Write access 
            public void WriteTextWriter1(
                [TestStream("path", FileAccess.Write)] TextWriter tw
                )
            {
                tw.Write(_writeMessage);
            }

            // When FileAccess it not specified, we try to figure it out via the parameter type. 
            public void WriteTextWriter2(
                    [TestStream] TextWriter tw
                )
            {
                tw.Write(_writeMessage);
            }

            // When FileAccess it not specified, we try to figure it out via the parameter type. 
            public void WriteTextWriter3(
                    [TestStream] TextWriter tw
                )
            {
                tw.Write(_writeMessage);
                tw.Flush(); // Extra flush 
            }

            public void WriteString(
                [TestStream] out string x
                )
            {
                x = _writeMessage;
            }

            public void WriteByteArray(
                [TestStream] out byte[] x
                )
            {
                x = Encoding.UTF8.GetBytes(_writeMessage);
            }


            #endregion // #region Write overloads 

            public async Task<Stream> ConvertAsync(TestStreamAttribute input, CancellationToken cancellationToken)
            {
                if (input.Access == FileAccess.Read)
                {
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes("Hello"));
                    stream.Position = 0;
                    return stream;
                }
                if (input.Access == FileAccess.Write)
                {
                    var stream = new MemoryStream();
                    _writeStream = stream;
                    return stream;
                }
                throw new NotImplementedException();
            }
        }

        // From a JObject (ala the Function.json), generate a strongly-typed attribute. 
        [Fact]
        public void TestMetadata()
        {
            JobHostConfiguration config = TestHelpers.NewConfig();
            var host2 = new JobHost(config);
            var metadataProvider = host2.CreateMetadataProvider();

            // Blob 
            var blobAttr = GetAttr<TestStreamAttribute>(metadataProvider, new { path = "x" });
            Assert.Equal("x", blobAttr.Path);

            // Special casing to map Direction to Access field. 
            blobAttr = GetAttr<TestStreamAttribute>(metadataProvider, new { path = "x", direction = "in" });
            Assert.Equal("x", blobAttr.Path);
            Assert.Equal(FileAccess.Read, blobAttr.Access);

            blobAttr = GetAttr<TestStreamAttribute>(metadataProvider, new { Path = "x", Direction = "out" });
            Assert.Equal("x", blobAttr.Path);
            Assert.Equal(FileAccess.Write, blobAttr.Access);

            blobAttr = GetAttr<TestStreamAttribute>(metadataProvider, new { path = "x", direction = "inout" });
            Assert.Equal("x", blobAttr.Path);
            Assert.Equal(FileAccess.ReadWrite, blobAttr.Access);
        }

        // Verify that we get Default Type to stream 
        [Fact]
        public void DefaultType()
        {
            var config = TestHelpers.NewConfig<ConfigNullOutParam>();
            config.AddExtension(new ConfigNullOutParam()); // Registers a BindToInput rule
            var host = new JobHost(config);
            IJobHostMetadataProvider metadataProvider = host.CreateMetadataProvider();

            // Getting default type. 
            var attr = new TestStreamAttribute("x", FileAccess.Read);
            {
                var defaultType = metadataProvider.GetDefaultType(attr, FileAccess.Read, null);
                Assert.Equal(typeof(Stream), defaultType);
            }

            {
                var defaultType = metadataProvider.GetDefaultType(attr, FileAccess.Write, null);
                Assert.Equal(typeof(Stream), defaultType);
            }
        }


        static T GetAttr<T>(IJobHostMetadataProvider metadataProvider, object obj) where T : Attribute
        {
            var attribute = metadataProvider.GetAttribute(typeof(T), JObject.FromObject(obj));
            return (T)attribute;
        }

        // Glue to initialize a JobHost with the correct config and invoke the Test method. 
        // Config also has the program on it.         
        private void TestWorker<TConfig>() where TConfig : IExtensionConfigProvider, ITest<TConfig>, new()
        {
            var prog = new TConfig();
            var jobActivator = new FakeActivator();
            jobActivator.Add(prog);

            var appSettings = new FakeNameResolver();
            appSettings.Add("y", "123");

            IExtensionConfigProvider ext = prog;
            var host = TestHelpers.NewJobHost<TConfig>(jobActivator, ext, appSettings);

            ITest<TConfig> test = prog;
            test.Test(host);
        }
    }
}
