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

        private FileAccess _access; // Which direction this rule applies to. 
        private readonly INameResolver _nameResolver;
        private readonly IConverterManager _converterManager;
        private readonly PatternMatcher _patternMatcher;

        public BindToStreamBindingProvider(PatternMatcher patternMatcher, FileAccess access)
        {
            _patternMatcher = patternMatcher;
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


        // $$$ Can this merge with the IValueBinder?

        private abstract class ArgHelper
        {
            public Stream _inner; 
            public abstract object Get();
            public virtual Task Flush() { return Task.CompletedTask; }
            // public bool IsRead; ??? $$$ 
        }

        private class StreamArgHelper : ArgHelper
        {
            public override object Get()
            {
                return _inner;
            }
        }
     
        private class TextReaderArgHelper : ArgHelper
        {
            private TextReader _arg;
            public override object Get()
            {
                _arg = new StreamReader(_inner);
                return _arg;
            }
            public override Task Flush()
            {
                _arg.Close(); // $$$ Closes underlying stream 
                return Task.CompletedTask; 
            }
        }

        private class TextWriterArgHelper : ArgHelper
        {
            private TextWriter _arg;
            public override object Get()
            {
                _arg = new StreamWriter(_inner);
                return _arg;
            }
            public override Task Flush()
            {
                return _arg.FlushAsync();
            }
        }

        private class StringArgHelper : ArgHelper
        {
            public override object Get()
            {
                using (var arg = new StreamReader(_inner))
                {
                    return arg.ReadToEnd(); // $$$ async?
                }
            }            
        }
         
        // $$$ Merge with 
        private class ByteArrayArgHelper : ArgHelper
        {
            public override object Get()
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    const int DefaultBufferSize = 4096;
                    _inner.CopyTo(outputStream, DefaultBufferSize); // $$$ Make async
                    byte[] value = outputStream.ToArray();
                    return value;
                }
            }
        }

        private class OutStringArgHelper : ArgHelper
        {
            public override object Get()
            {
                return null; // Out-parameter only 
            }
            public override Task Flush() // $$$ need th evalue...
            {
                return base.Flush();
            }
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
                        argHelperType = typeof(StreamArgHelper);
                        isRead = true;
                        
                        break;
                    case FileAccess.Write:
                        argHelperType = typeof(StreamArgHelper);
                        isRead = false;
                        break;

                    default:
                        throw new NotImplementedException("ReadWrite access is not supported. Pick either Read or Write.");
                }
            }
            else if (typeUser == typeof(TextReader))
            {
                argHelperType = typeof(TextReaderArgHelper);
                isRead = true;
            }
            else if (typeUser == typeof(String))
            {
                argHelperType = typeof(StringArgHelper);
                isRead = true;
            }
            else if (typeUser == typeof(byte[]))
            {
                argHelperType = typeof(ByteArrayArgHelper);
                isRead = true;
            }
            else if (typeUser == typeof(TextWriter))
            {
                argHelperType = typeof(TextWriterArgHelper);
                isRead = false;
            }
            else if (typeUser == typeof(String).MakeByRefType())
            {
                argHelperType = typeof(OutStringArgHelper);
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

        // Caller has already verified the rule is applicable.
        private class ReadExactBinding : BindingBase<TAttribute>
        {
            private readonly BindToStreamBindingProvider<TAttribute> _parent;
            private readonly Type _argHelperType;
            private readonly Type _userType;
            private readonly FileAccess _targetFileAccess;

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
                _argHelperType = argHelper;
                _userType = userType;
                _targetFileAccess = targetFileAccess;
            }

            protected override Task<IValueProvider> BuildAsync(TAttribute attrResolved, ValueBindingContext context)
            {
                // set FileAccess beofre calling into the converter. Don't want converters to need to deal with a null FileAccess.
                SetFileAccessFromAttribute(attrResolved, _targetFileAccess);

                var patternMatcher = _parent._patternMatcher;
                var argHelper = (ArgHelper)Activator.CreateInstance(_argHelperType);


                var builder = patternMatcher.TryGetConverterFunc(typeof(TAttribute), typeof(Stream));

                var stream = (Stream) builder(attrResolved);
                argHelper._inner = stream;

                var obj = argHelper.Get();

                // Coerce 
                var valueProvider = new ReaderValueProvider
                {
                    _helper = argHelper,
                    Type = _userType,
                    _stream = stream,
                    _obj = obj,
                    _invokeString = "???"
                };
                return Task.FromResult<IValueProvider>(valueProvider);
            }
        }

        private class ReaderValueProvider : IValueBinder
        {
            public ArgHelper _helper;
            public Stream _stream;
            public object _obj;
            public Type Type { get; set; }
            public string _invokeString;

            public Task<object> GetValueAsync()
            {
                return Task.FromResult(_obj);
            }

            public async Task SetValueAsync(object value, CancellationToken cancellationToken)
            {
                // Caller has done some degree of type-verifications 
                // $$$ verify matches?


                // $$$ CloudBlob implementation will 
                // - notify other watchers (optimization for BlobTrigger) 
                // - Call CloudBlobStream.Commit() 
                await _helper.Flush();
                _stream.Close(); // $$$ Is this more complex? 
            }

            public string ToInvokeString()
            {
                return _invokeString;
            }
        }
    }
}
