// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Indexers;

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
        private readonly IConverterManager _converterManager;
        private readonly PatternMatcher _patternMatcher;

        public BindToStreamBindingProvider(PatternMatcher patternMatcher, FileAccess access, INameResolver nameResolver, IConverterManager converterManager, IExtensionTypeLocator extensionTypeLocator)
        {
            _patternMatcher = patternMatcher;
            _nameResolver = nameResolver;
            _converterManager = converterManager;
            _access = access;

            Add(extensionTypeLocator, (ConverterManager) _converterManager);
        }

        #region Hackery for ExtensionLocator

        // create IConverterManager adapters to any legacy ICloudBlobStreamBinder<T>. 
        static void Add(IExtensionTypeLocator extensionTypeLocator, ConverterManager cm)
        {
            if (extensionTypeLocator == null)
            {
                return;
            }

            foreach (var type in extensionTypeLocator.GetCloudBlobStreamBinderTypes())
            {
                var inst = Activator.CreateInstance(type);

                var t = Blobs.CloudBlobStreamObjectBinder.GetBindingValueType(type);
                var m = typeof(BindToStreamBindingProvider<TAttribute>).GetMethod("AddAdapter", BindingFlags.Static | BindingFlags.NonPublic);
                m = m.MakeGenericMethod(t);
                m.Invoke(null, new object[] { cm, inst });
            }
        }

        static void AddAdapter<T>(ConverterManager cm, ICloudBlobStreamBinder<T> x)
        {
            cm.AddExactConverter<Stream, T>(stream => x.ReadFromStreamAsync(stream, CancellationToken.None).Result);

            cm.AddExactConverter<ToStream<T>, object>(pair =>
            {
                T value = pair.Value;
                Stream stream = pair.Stream;
                x.WriteToStreamAsync(value, stream, CancellationToken.None).Wait();
                return null;
            });
        }
        #endregion


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
                    UserType = OpenType.FromType(type)
                };
            }
        }

        static private void VerifyAccessOrThrow(FileAccess? declaredAccess, bool isRead)
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
            var typeUser = parameter.ParameterType;

            if (typeUser.IsByRef)
            {
                typeUser = typeUser.GetElementType(); // Can't generic instantiate  a ByRef. 
            }
                        
            var type = typeof(StreamBinding);
            var method = type.GetMethod("TryBuild", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            method = method.MakeGenericMethod(typeUser);
            var binding = BindingFactoryHelpers.MethodInvoke<IBinding>(method, this, context);

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
        private class StreamBinding : BindingBase<TAttribute>
        {
            private readonly BindToStreamBindingProvider<TAttribute> _parent;
            private readonly Type _userType;
            private readonly FileAccess _targetFileAccess;
            private readonly Type _typeValueProvider;

            public StreamBinding(
                AttributeCloner<TAttribute> cloner,
                ParameterDescriptor param,
                BindToStreamBindingProvider<TAttribute> parent,
                Type argHelper,
                Type userType,
                FileAccess targetFileAccess,
                object converterParam)
                    : base(cloner, param)
            {
                _parent = parent;
                _userType = userType;
                _targetFileAccess = targetFileAccess;
                _typeValueProvider = argHelper;
                _converter = converterParam;
            }


            public static IBinding TryBuild<TUserType>(
                BindToStreamBindingProvider<TAttribute> parent,
                BindingProviderContext context)
            {
                // Allowed Param types:
                //  Stream
                //  any T with a Stream --> T conversion 
                // out T, with a Out<Stream,T> --> void conversion 

                var parameter = context.Parameter;
                var parameterType = parameter.ParameterType;


                var attributeSource = TypeUtility.GetResolvedAttribute<TAttribute>(parameter);

                // Stream is either way; all other types are known. 
                FileAccess? declaredAccess = GetFileAccessFromAttribute(attributeSource);

                Type argHelperType;
                bool isRead;

                IConverterManager cm = parent._converterManager;
                INameResolver nm = parent._nameResolver;

                object converterParam = null;
                {
                    if (parameter.IsOut)
                    {
                        var outConverter = cm.GetConverter<ToStream<TUserType>, object, TAttribute>();
                        if (outConverter != null)
                        {
                            converterParam = outConverter;
                            isRead = false;
                            argHelperType = typeof(OutArgBaseValueProvider<>).MakeGenericType(typeof(TAttribute), typeof(TUserType));
                        }
                        else
                        {
                            throw new InvalidOperationException($"No stream converter to handle {typeof(TUserType).FullName}.");
                        }
                    }
                    else
                    {
                        var converter = cm.GetConverter<Stream, TUserType, TAttribute>();
                        if (converter != null)
                        {
                            converterParam = converter;

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
                            }
                            else
                            {
                                // For backwards compat, we recognize TextWriter as write; 
                                // anything else should explicitly set the FileAccess flag. 
                                if (typeof(TextWriter).IsAssignableFrom(typeof(TUserType)))
                                {
                                    isRead = false;
                                }
                                else
                                {
                                    isRead = true;
                                }
                            }


                            argHelperType = typeof(ValueProvider<>).MakeGenericType(typeof(TAttribute), typeof(TUserType));
                        }
                        else
                        {
                            // This rule can't bind. 
                            // Let another try. 
                            return null;
                        }
                    }
                }

                VerifyAccessOrThrow(declaredAccess, isRead);
                if (!parent.IsSupportedByRule(isRead))
                {
                    return null;
                }

                var cloner = new AttributeCloner<TAttribute>(attributeSource, context.BindingDataContract, nm);

                ParameterDescriptor param;
                if (parent.BuildParameterDescriptor != null)
                {
                    param = parent.BuildParameterDescriptor(attributeSource, parameter, nm);
                }
                else
                {
                    param = new ParameterDescriptor
                    {
                        Name = parameter.Name,
                        DisplayHints = new ParameterDisplayHints
                        {
                            Description = isRead ? "Read Stream" : "Write Stream"
                        }
                    };
                }

                var fileAccess = isRead ? FileAccess.Read : FileAccess.Write;
                IBinding binding = new StreamBinding(cloner, param, parent, argHelperType, parameterType, fileAccess, converterParam);

                return binding;
            }


            object _converter;

            protected override async Task<IValueProvider> BuildAsync(TAttribute attrResolved, ValueBindingContext context)
            {
                // set FileAccess beofre calling into the converter. Don't want converters to need to deal with a null FileAccess.
                SetFileAccessFromAttribute(attrResolved, _targetFileAccess);

                var patternMatcher = _parent._patternMatcher;
                Func<object, object> builder = patternMatcher.TryGetConverterFunc(typeof(TAttribute), typeof(Stream));
                Func<Stream> buildStream = () => (Stream)builder(attrResolved);

                BaseValueProvider valueProvider = (BaseValueProvider)Activator.CreateInstance(_typeValueProvider, _converter);
                var invokeString = this.Cloner.GetInvokeString(attrResolved);
                await valueProvider.InitAsync(buildStream, _userType, _parent, invokeString);

                return valueProvider;
            }
        }

        // The base IValueProvider. Handed  out per-instance
        // This wraps the stream and coerces it to the user's parameter.
        private abstract class BaseValueProvider : IValueBinder
        {
            protected BindToStreamBindingProvider<TAttribute> _parent;

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

            public async Task InitAsync(Func<Stream> builder, Type userType, BindToStreamBindingProvider<TAttribute> parent, string invokeString)
            {
                Type = userType;
                _invokeString = invokeString;
                _streamBuilder = builder;
                _parent = parent;

                _userValue = await this.CreateUserArgAsync();
            }

            public Task<object> GetValueAsync()
            {
                return Task.FromResult<object>(_userValue);
            }

            public virtual async Task SetValueAsync(object value, CancellationToken cancellationToken)
            {
                // 'Out T' parameters override this method; so this case only needs to handle normal input parameters. 

                // value may be null (such as in an IBinder.Bind case). 
                if (_userValue is IDisposable disposable)
                {
                    // This will handle TextWriter and a chance for other writers to flush their contents to the underlying stream.
                    // This needs to be done *before* we close the underlying stream, so we can't wait for webjobs 
                    // to do the check. 
                    disposable.Dispose();
                }
                                
                if (_stream != null)
                {
                    // 1. Close() / Dispose()  are sync. So we want to FlushAsync() first to do any long-running operations async,
                    //    and then the sync close() is just a nop. 
                    // 2. Stream may already be closed (in user's code). 
                    //    - Can't call Flush() on a closed stream. But CanWrite should be set to false once the stream is closed. 
                    //    - closed/disposed are safe to call multiple times.

                    if (_stream.CanWrite)
                    {
                        // Do heavy lifting as async so the following close() is a nop. 
                        await _stream.FlushAsync();
                    }
                    _stream.Close();
                }
            }

            public string ToInvokeString()
            {
                return _invokeString;
            }

            // Deterministic initialization for UserValue. 
            protected abstract Task<object> CreateUserArgAsync();
        }

        // Regular value provider that handles all non-out cases. 
        // This coerces the Stream to the user parameter type. 
        private class ValueProvider<TUserType> : BaseValueProvider
        {
            private readonly FuncAsyncConverter<Stream, TUserType> _converter;

            public ValueProvider(object converter)
            {
                _converter = (FuncAsyncConverter<Stream, TUserType>)converter;
            }                       

            protected override async Task<object> CreateUserArgAsync()
            {
                TUserType result;
                var stream = this.GetOrCreateStream();
                if (stream == null)
                {
                    // If T is a struct, then we need to create a value for it.
                    result = default(TUserType);
                }
                else
                {
                    result = await _converter(stream, null, null);
                }
                return result;
            }
        }
                
        // Base class for 'out T' stream bindings. 
        // These are special in that they don't create the stream until after the function returns. 
        private class OutArgBaseValueProvider<TUserType> : BaseValueProvider
        {
            private readonly FuncAsyncConverter<ToStream<TUserType>, object> _converter;

            public OutArgBaseValueProvider(object converter)
            {
                _converter = (FuncAsyncConverter<ToStream<TUserType>, object>)converter;
            }

            override protected Task<object> CreateUserArgAsync()
            {
                // Nop on create. Will do work on complete. 
                var val = default(TUserType);
                return Task.FromResult<object>(val);
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
                    var pair = new ToStream<TUserType>
                    {
                        Value = (TUserType) value,
                         Stream = stream
                    };
                    await _converter(pair, null, null); // will write to the stream. 
                } // Dispose on Stream will close it. Safe to call this multiple times. 
            }
        }
    }
}
