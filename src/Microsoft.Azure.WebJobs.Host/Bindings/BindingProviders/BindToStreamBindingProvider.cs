// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    // BindToStream.
    // Read: Stream, TextReader, string,  byte[]
    // Write: Stream, TextWriter, out string,  out byte[]
    internal class BindToStreamBindingProvider<TAttribute> :
        FluentBindingProvider<TAttribute>,
        IBindingProvider,
        IBindingRuleProvider
        where TAttribute : Attribute
    {
        private readonly FileAccess _access; // Which direction this rule applies to. Can be R, W, or  RW
        private readonly INameResolver _nameResolver;
        private readonly PatternMatcher _patternMatcher;

        public BindToStreamBindingProvider(PatternMatcher patternMatcher, FileAccess access, INameResolver nameResolver)
        {
            _patternMatcher = patternMatcher;
            _nameResolver = nameResolver;
            _access = access;
        }

        public Type GetDefaultType(Attribute attribute, FileAccess access, Type requestedType)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<BindingRule> GetRules()
        {
            yield break; // $$$ Add some 
        }

        private void VerifyOrThrow(FileAccess? actualAccess, bool isRead)
        {
            // Verify direction is compatible with the attribute's direction flag. 
            if (actualAccess.HasValue)
            {
                string errorMsg = null;
                if (isRead)
                {
                    if (!CanRead(actualAccess.Value))
                    {
                        errorMsg = "Read";
                    }
                }
                else
                {
                    if (!CanWrite(actualAccess.Value))
                    {
                        errorMsg = "Write";
                    }
                }
                if (errorMsg != null)
                {
                    throw new InvalidOperationException($"The parameter type is a '{errorMsg}' binding, but the Attribute's access type is '{actualAccess}'");
                }
            }
        }

        // Return true iff this rule can support the given mode. 
        // Returning false allows another rule to handle this. 
        private bool IsSupportedByRule(bool isRead)
        { 
            // Verify the expected binding is supported by this rule
            if (isRead)
            {
                if (!CanRead(_access))
                {
                    // $$$ Would be good to give an error here, but could be blank since another rule is claiming it. 
                    return false;
                }
            }
            else // isWrite
            {
                if (!CanWrite(_access))
                {
                    return false;
                }
            }
            return true;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var parameter = context.Parameter;
            var typeUser = parameter.ParameterType;

            var attributeSource = TypeUtility.GetResolvedAttribute<TAttribute>(parameter);

            // Stream is either way; all other types are known. 
            FileAccess? actualAccess = GetFileAccessFromAttribute(attributeSource);

            Type argHelperType;
            bool isRead;
            if (typeUser == typeof(Stream))
            {
                if (!actualAccess.HasValue)
                {
                    throw new InvalidOperationException("When binding to Stream, the attribute must specify a FileAccess direction.");
                }
                switch (actualAccess.Value)
                {
                    case FileAccess.Read:
                        isRead = true;
                        
                        break;
                    case FileAccess.Write: 
                        isRead = false;
                        break;

                    default:
                        throw new NotImplementedException("ReadWrite access is not supported. Pick either Read or Write.");
                }
                argHelperType = typeof(StreamValueProvider);
            }
            else if (typeUser == typeof(TextReader))
            {
                argHelperType = typeof(TextReaderValueProvider);
                isRead = true;
            }
            else if (typeUser == typeof(String))
            {
                argHelperType = typeof(StringValueProvider);
                isRead = true;
            }
            else if (typeUser == typeof(byte[]))
            {
                argHelperType = typeof(ByteArrayValueProvider);
                isRead = true;
            }
            else if (typeUser == typeof(TextWriter))
            {
                argHelperType = typeof(TextWriterValueProvider);
                isRead = false;
            }
            else if (typeUser == typeof(String).MakeByRefType())
            {
                argHelperType = typeof(OutStringArgBaseProvider);
                isRead = false;
            }
            else if (typeUser == typeof(byte[]).MakeByRefType())
            {
                argHelperType = typeof(OutByteArrayArgBaseProvider);
                isRead = false;
            }
            else
            {
                // Totally unrecognized. Let another binding try it. 
                return Task.FromResult<IBinding>(null);
            }

            VerifyOrThrow(actualAccess, isRead);
            if (!IsSupportedByRule(isRead))
            {
                return Task.FromResult<IBinding>(null);
            }
            
            var cloner = new AttributeCloner<TAttribute>(attributeSource, context.BindingDataContract, _nameResolver);

            var param = new ParameterDescriptor
            {
                Name = parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                    Description = isRead ? "Read Stream" : "Write Stream"
                }
            };

            var fileAccess = isRead ? FileAccess.Read : FileAccess.Write;
            IBinding binding = new ReadExactBinding(cloner, param, this, argHelperType, typeUser, fileAccess);
            
            return Task.FromResult<IBinding>(binding);
        }

        private static bool CanRead(FileAccess access)
        {
            return access != FileAccess.Write;
        }
        private static bool CanWrite(FileAccess access)
        {
            return access != FileAccess.Read;
        }


        private static FileAccess? GetFileAccessFromAttribute(Attribute attribute)
        {
            var prop = attribute.GetType().GetProperty("Access", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                // $$$ Check type 
                throw new InvalidOperationException("The BindToStream rule requires that attributes have an Access property of type 'FileAccess?'");
            }

            var val = prop.GetValue(attribute);
            var access = (FileAccess?)val;
                        
            return access.Value;
        }

        private static void SetFileAccessFromAttribute(Attribute attribute, FileAccess access)
        {
            var prop = attribute.GetType().GetProperty("Access", BindingFlags.Public | BindingFlags.Instance);
            prop.SetValue(attribute, access); // $$$ FileAccess? vs FileAccess
        }
                
        // As a binding, this is one per parameter, shared across each invocation instance.
        private class ReadExactBinding : BindingBase<TAttribute>
        {
            private readonly BindToStreamBindingProvider<TAttribute> _parent;
            private readonly Type _userType;
            private readonly FileAccess _targetFileAccess;
            private readonly Type _typeValueProvider;

            public ReadExactBinding(
                AttributeCloner<TAttribute> cloner,
                ParameterDescriptor param,
                BindToStreamBindingProvider<TAttribute> parent,
                Type argHelper,
                Type userType,
                FileAccess targetFileAccess)
                : base(cloner, param)
            {
                _parent = parent;
                _userType = userType;
                _targetFileAccess = targetFileAccess;
                _typeValueProvider = argHelper;
            }

            protected override async Task<IValueProvider> BuildAsync(TAttribute attrResolved, ValueBindingContext context)
            {
                // set FileAccess beofre calling into the converter. Don't want converters to need to deal with a null FileAccess.
                SetFileAccessFromAttribute(attrResolved, _targetFileAccess);

                var patternMatcher = _parent._patternMatcher;
                Func<object, object> builder = patternMatcher.TryGetConverterFunc(typeof(TAttribute), typeof(Stream));
                Func<Stream> builder2 = () => (Stream)builder(attrResolved);


                BaseProvider valueProvider = (BaseProvider)Activator.CreateInstance(_typeValueProvider);
                await valueProvider.InitAsync(builder2, _userType);
             
                return valueProvider;
            }
        }

        #region Out parameters
        // Base class for 'out T' stream bindings. 
        // These are special in that they don't create the stream until after the function functions. 
        private abstract class OutArgBaseProvider : BaseProvider
        {
            override protected Task<object> CreateUserArgAsync()
            {
                // Nop on create. Will do work on complete. 
                return Task.FromResult<object>(null);
            }

            public override async Task SetValueAsync(object value, CancellationToken cancellationToken)
            {
                // Normally value is the same as the input value. 
                if (value == null)
                {
                    // This means we're an 'out T' parameter and they left it null.
                    // Don't create the stream or write anything in this case. 
                    return;
                }

                // Now Create the stream 
                using (var stream = this.GetOrCreateStream())
                {
                    await this.WriteToStreamAsync(value, cancellationToken);
                } // Dipose on Stream will close it. Safe to call this multiple times. 
            }

            protected abstract Task WriteToStreamAsync(object value, CancellationToken cancellationToken);
        }

        private class OutStringArgBaseProvider : OutArgBaseProvider
        {
            protected override async Task WriteToStreamAsync(object value, CancellationToken cancellationToken)
            {
                var stream = this.GetOrCreateStream();

                var text = (string)value;

                const int DefaultBufferSize = 1024;

                var encoding = new UTF8Encoding(false); // skip emitting BOM

                using (TextWriter writer = new StreamWriter(stream, encoding, DefaultBufferSize,
                        leaveOpen: true))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await writer.WriteAsync(text);
                }
            }
        }

        private class OutByteArrayArgBaseProvider : OutArgBaseProvider
        {
            protected override async Task WriteToStreamAsync(object value, CancellationToken cancellationToken)
            {
                var stream = this.GetOrCreateStream();
                var bytes = (byte[])value;
                await stream.WriteAsync(bytes, 0, bytes.Length);
            }
        }
        #endregion // Out parameters


        // The base IVlaueProvider. Handed  out per-instance
        // This wraps the stream and coerces it to the user's parameter.
        private abstract class BaseProvider : IValueBinder
        {
            private Stream _stream;
            public Type Type { get; set; } // Impl IValueBinder

            private string _invokeString;

            // Helper to build the stream. This will only get invoked once and then cached as _stream. 
            private Func<Stream> _streamBuilder;

            object _userArg;

            protected Stream GetOrCreateStream()
            {
                if (_stream == null)
                {
                    _stream = _streamBuilder();
                }
                return _stream;
            }

            public async Task InitAsync(Func<Stream> builder, Type userType)
            {
                Type = userType;
                _invokeString = "???";
                _streamBuilder = builder;

                _userArg = await this.CreateUserArgAsync();
            }

            public Task<object> GetValueAsync()
            {
                return Task.FromResult<object>(_userArg);
            }
            
            public virtual async Task SetValueAsync(object value, CancellationToken cancellationToken)
            {
                // 'Out T' parameters override this method; so this case only needs to handle normal input parameters. 
                await this.FlushAsync();
                _stream.Close(); // Safe to call this multiple times. 
            }

            public string ToInvokeString()
            {
                return _invokeString;
            }
                       
            // Deterministic initialization for UserValue. 
            abstract protected Task<object> CreateUserArgAsync();
            
            // Give derived object a chance to flush any buffering before we close the stream. 
            virtual protected Task FlushAsync() { return Task.CompletedTask;  }
        }

        // Runs both ways
        private class StreamValueProvider : BaseProvider
        {
            protected override Task<object> CreateUserArgAsync()
            {
                return Task.FromResult<object>(this.GetOrCreateStream());
            }
        }

        private class TextReaderValueProvider : BaseProvider
        {
            protected override Task<object> CreateUserArgAsync()
            {
                var stream = this.GetOrCreateStream();
                var arg = new StreamReader(stream);
                return Task.FromResult<object>(arg);
            }    
        }

        private class StringValueProvider : BaseProvider
        {
            protected override async Task<object> CreateUserArgAsync()
            {
                var stream = this.GetOrCreateStream();
                using (var arg = new StreamReader(stream))
                {
                    var str = await arg.ReadToEndAsync();
                    return str;
                }
            }
        }

        private class ByteArrayValueProvider : BaseProvider
        {
            protected override async Task<object> CreateUserArgAsync()
            {
                var stream = this.GetOrCreateStream();
                using (MemoryStream outputStream = new MemoryStream())
                {
                    const int DefaultBufferSize = 4096;
                    await stream.CopyToAsync(outputStream, DefaultBufferSize);
                    byte[] value = outputStream.ToArray();
                    return value;
                }
            }
        }

        private class TextWriterValueProvider : BaseProvider
        {
            private TextWriter _arg;

            protected override Task<object> CreateUserArgAsync()
            {
                var stream = this.GetOrCreateStream();
                _arg = new StreamWriter(stream);
                return Task.FromResult<object>(_arg);
            }

            protected override async Task FlushAsync()
            {
                await _arg.FlushAsync();
            }
        }
    }
}
