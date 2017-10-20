// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;

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
            if (attribute is TAttribute)
            {
                return typeof(Stream);
            }
            return null;
        }

        public IEnumerable<BindingRule> GetRules()
        {
            foreach (var type in new Type[]
            {
                typeof(Stream),
                typeof(TextReader),
                typeof(TextWriter),
                typeof(string),
                typeof(byte[]),
                typeof(string).MakeByRefType(),
                typeof(byte[]).MakeByRefType()
            })
            {
                yield return new BindingRule
                {
                    SourceAttribute = typeof(TAttribute),
                    UserType = new ConverterManager.ExactMatch(type)
                };
            }
        }

        private void VerifyAccessOrThrow(FileAccess? declaredAccess, bool isRead)
        {
            // Verify direction is compatible with the attribute's direction flag. 
            if (declaredAccess.HasValue)
            {
                string errorMsg = null;
                if (isRead)
                {
                    if (!CanRead(declaredAccess.Value))
                    {
                        errorMsg = "Read";
                    }
                }
                else
                {
                    if (!CanWrite(declaredAccess.Value))
                    {
                        errorMsg = "Write";
                    }
                }
                if (errorMsg != null)
                {
                    throw new InvalidOperationException($"The parameter type is a '{errorMsg}' binding, but the Attribute's access type is '{declaredAccess}'");
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
                    // Would be good to give an error here, but could be blank since another rule is claiming it. 
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
            var parameterType = parameter.ParameterType;

            var attributeSource = TypeUtility.GetResolvedAttribute<TAttribute>(parameter);

            // Stream is either way; all other types are known. 
            FileAccess? declaredAccess = GetFileAccessFromAttribute(attributeSource);

            Type argHelperType;
            bool isRead;
            if (parameterType == typeof(Stream))
            {
                if (!declaredAccess.HasValue)
                {
                    throw new InvalidOperationException("When binding to Stream, the attribute must specify a FileAccess direction.");
                }
                switch (declaredAccess.Value)
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
            else if (parameterType == typeof(TextReader))
            {
                argHelperType = typeof(TextReaderValueProvider);
                isRead = true;
            }
            else if (parameterType == typeof(String))
            {
                argHelperType = typeof(StringValueProvider);
                isRead = true;
            }
            else if (parameterType == typeof(byte[]))
            {
                argHelperType = typeof(ByteArrayValueProvider);
                isRead = true;
            }
            else if (parameterType == typeof(TextWriter))
            {
                argHelperType = typeof(TextWriterValueProvider);
                isRead = false;
            }
            else if (parameterType == typeof(String).MakeByRefType())
            {
                argHelperType = typeof(OutStringValueProvider);
                isRead = false;
            }
            else if (parameterType == typeof(byte[]).MakeByRefType())
            {
                argHelperType = typeof(OutByteArrayValueProvider);
                isRead = false;
            }
            else
            {
                // Totally unrecognized. Let another binding try it. 
                return Task.FromResult<IBinding>(null);
            }

            VerifyAccessOrThrow(declaredAccess, isRead);
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
            IBinding binding = new ReadExactBinding(cloner, param, this, argHelperType, parameterType, fileAccess);

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

        private static PropertyInfo GetFileAccessProperty(Attribute attribute)
        {
            var prop = attribute.GetType().GetProperty("Access", BindingFlags.Public | BindingFlags.Instance);
            return prop;
        }

        private static FileAccess? GetFileAccessFromAttribute(Attribute attribute)
        {
            var prop = GetFileAccessProperty(attribute);

            if (prop != null)
            {
                if ((prop.PropertyType != typeof(FileAccess?) && (prop.PropertyType != typeof(FileAccess))))
                {
                    prop = null;
                }
            }
            if (prop == null)
            {
                throw new InvalidOperationException("The BindToStream rule requires that attributes have an Access property of type 'FileAccess?' or 'FileAccess'");
            }

            var val = prop.GetValue(attribute);
            var access = (FileAccess?)val;

            return access;
        }

        private static void SetFileAccessFromAttribute(Attribute attribute, FileAccess access)
        {
            var prop = GetFileAccessProperty(attribute);
            // We already verified the type in GetFileAccessFromAttribute
            prop.SetValue(attribute, access);
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
                Func<Stream> buildStream = () => (Stream)builder(attrResolved);

                BaseValueProvider valueProvider = (BaseValueProvider)Activator.CreateInstance(_typeValueProvider);
                await valueProvider.InitAsync(buildStream, _userType);

                return valueProvider;
            }
        }

        // The base IValueProvider. Handed  out per-instance
        // This wraps the stream and coerces it to the user's parameter.
        private abstract class BaseValueProvider : IValueBinder
        {
            private Stream _stream; // underlying stream 
            private object _userValue; // argument passed to the user's function. This is some wrapper over _stream. 
            private string _invokeString;

            // Helper to build the stream. This will only get invoked once and then cached as _stream. 
            private Func<Stream> _streamBuilder;

            public Type Type { get; set; } // Implement IValueBinder.Type

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

                _userValue = await this.CreateUserArgAsync();
            }

            public Task<object> GetValueAsync()
            {
                return Task.FromResult<object>(_userValue);
            }

            public virtual async Task SetValueAsync(object value, CancellationToken cancellationToken)
            {
                // 'Out T' parameters override this method; so this case only needs to handle normal input parameters. 
                await this.FlushAsync();
                if (_stream != null)
                {
                    // These are safe even when the stream is closed/disposed. 
                    //await _stream.FlushAsync();
                    _stream.Close(); // Safe to call this multiple times. 
                }
            }

            public string ToInvokeString()
            {
                return _invokeString;
            }

            // Deterministic initialization for UserValue. 
            protected abstract Task<object> CreateUserArgAsync();

            // Give derived object a chance to flush any buffering before we close the stream. 
            protected virtual Task FlushAsync() { return Task.CompletedTask; }
        }

        // Bind to a 'Stream' parameter.  Handles both Read and Write streams.
        private class StreamValueProvider : BaseValueProvider
        {
            protected override Task<object> CreateUserArgAsync()
            {
                return Task.FromResult<object>(this.GetOrCreateStream());
            }
        }

        // Bind to a 'TextReader' parameter.
        private class TextReaderValueProvider : BaseValueProvider
        {
            protected override Task<object> CreateUserArgAsync()
            {
                var stream = this.GetOrCreateStream();
                if (stream == null)
                {
                    return Task.FromResult<object>(null);
                }
                var arg = new StreamReader(stream);
                return Task.FromResult<object>(arg);
            }
        }

        // Bind to a 'String' parameter. 
        // This reads the entire contents on invocation and passes as a single string. 
        private class StringValueProvider : BaseValueProvider
        {
            protected override async Task<object> CreateUserArgAsync()
            {
                var stream = this.GetOrCreateStream();
                if (stream == null)
                {
                    return null;
                }
                using (var arg = new StreamReader(stream))
                {
                    var str = await arg.ReadToEndAsync();
                    return str;
                }
            }
        }

        // bind to a 'byte[]' parameter.
        // This reads the entire stream contents on invocation and passes as a byte[].
        private class ByteArrayValueProvider : BaseValueProvider
        {
            protected override async Task<object> CreateUserArgAsync()
            {
                var stream = this.GetOrCreateStream();
                if (stream == null)
                {
                    return null;
                }
                using (MemoryStream outputStream = new MemoryStream())
                {
                    const int DefaultBufferSize = 4096;
                    await stream.CopyToAsync(outputStream, DefaultBufferSize);
                    byte[] value = outputStream.ToArray();
                    return value;
                }
            }
        }

        // Bind to a 'TextWriter' parameter. 
        // This is for writing out to the stream. 
        private class TextWriterValueProvider : BaseValueProvider
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

        #region Out parameters
        // Base class for 'out T' stream bindings. 
        // These are special in that they don't create the stream until after the function returns. 
        private abstract class OutArgBaseValueProvider : BaseValueProvider
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

        // Bind to an 'out string' parameter
        private class OutStringValueProvider : OutArgBaseValueProvider
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

        // Bind to an 'out byte[]' parameter
        private class OutByteArrayValueProvider : OutArgBaseValueProvider
        {
            protected override async Task WriteToStreamAsync(object value, CancellationToken cancellationToken)
            {
                var stream = this.GetOrCreateStream();
                var bytes = (byte[])value;
                await stream.WriteAsync(bytes, 0, bytes.Length);
            }
        }
        #endregion // Out parameters
    }
}
