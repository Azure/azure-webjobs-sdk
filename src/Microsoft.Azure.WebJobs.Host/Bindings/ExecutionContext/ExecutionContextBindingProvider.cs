// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// This provider provides a binding to Type <see cref="ExecutionContext"/>.
    /// </summary>
    internal class ExecutionContextBindingProvider : IBindingProvider
    {
        private readonly IOptions<ExecutionContextOptions> _options;

        public ExecutionContextBindingProvider(IOptions<ExecutionContextOptions> options)
        {
            _options = options;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (context.Parameter.ParameterType != typeof(ExecutionContext))
            {
                return Task.FromResult<IBinding>(null);
            }

            return Task.FromResult<IBinding>(new ExecutionContextBinding(context.Parameter, _options));
        }

        private class ExecutionContextBinding : IBinding
        {
            private readonly ParameterInfo _parameter;
            private readonly IOptions<ExecutionContextOptions> _options;
            private static ParameterDisplayHints _displayHints = new ParameterDisplayHints { Description = "Function ExecutionContext" };

            public ExecutionContextBinding(ParameterInfo parameter, IOptions<ExecutionContextOptions> options)
            {
                _parameter = parameter;
                _options = options;
            }

            public bool FromAttribute
            {
                get { return false; }
            }

            public Task<IValueProvider> BindAsync(BindingContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                return Task.FromResult<IValueProvider>(new ExecutionContextValueProvider(context.ValueContext, _options));
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                return Task.FromResult<IValueProvider>(new ExecutionContextValueProvider(context, _options));
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor
                {
                    Name = _parameter.Name,
                    DisplayHints = _displayHints
                };
            }

            private class ExecutionContextValueProvider : IValueProvider
            {
                private readonly ValueBindingContext _context;
                private readonly IOptions<ExecutionContextOptions> _options;
                // TODO: Is this valid, or can something change it via Directory.SetCurrentDirectory
                // This is ~4.5% of our allocations vs. the 4.x baseline and 3.3.x lib
                // Source: https://github.com/dotnet/runtime/blob/b201a16e1a642f9532c8ea4e42d23af8f4484a36/src/libraries/System.Private.CoreLib/src/System/Environment.Windows.cs#L13-L39
                private static readonly string _currentDirectory = Environment.CurrentDirectory;
                private ExecutionContext _executionContext;

                public ExecutionContextValueProvider(ValueBindingContext context, IOptions<ExecutionContextOptions> options)
                {
                    _context = context;
                    _options = options;
                }

                public Type Type
                {
                    get { return typeof(ExecutionContext); }
                }

                public Task<object> GetValueAsync()
                {
                    // TODO: Is there any reason we can't cache this? It's not immutable today, so need to check
                    return Task.FromResult<object>(_executionContext ??= CreateContext());
                }

                public string ToInvokeString()
                {
                    return _context.FunctionInstanceId.ToString();
                }

                private ExecutionContext CreateContext()
                {
                    var result = new ExecutionContext
                    {
                        InvocationId = _context.FunctionInstanceId,
                        FunctionName = _context.FunctionContext.MethodName,
                        FunctionDirectory = _currentDirectory,
                        FunctionAppDirectory = _options.Value.AppDirectory,
                        RetryContext = _context.FunctionContext.RetryContext
                    };

                    if (result.FunctionAppDirectory != null)
                    {
                        result.FunctionDirectory = System.IO.Path.Combine(result.FunctionAppDirectory, result.FunctionName);
                    }

                    return result;
                }
            }
        }
    }
}