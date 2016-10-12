// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    // $$$
    internal delegate IValueProvider FuncArgumentBuilder<TAttribute>(TAttribute attribute, ValueBindingContext context);

    /// <summary>
    /// Converter  $$$
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    /// <typeparam name="TAttribute"></typeparam>
    /// <typeparam name="TDestination"></typeparam>
    /// <param name="src"></param>
    /// <param name="attribute"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public delegate TDestination FuncConverter<TSource, TAttribute, TDestination>(TSource src, TAttribute attribute, ValueBindingContext context)
            where TAttribute : Attribute;

    /// <summary>
    /// Provides binding context for all bind operations scoped to a particular
    /// function invocation.
    /// </summary>
    public class FunctionBindingContext
    {
        private readonly Guid _functionInstanceId;
        private readonly CancellationToken _functionCancellationToken;
        private readonly TraceWriter _trace;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="functionInstanceId">The instance ID of the function being bound to.</param>
        /// <param name="functionCancellationToken">The <see cref="CancellationToken"/> to use.</param>
        /// <param name="trace">The trace writer.</param>
        public FunctionBindingContext(Guid functionInstanceId, CancellationToken functionCancellationToken, TraceWriter trace)
        {
            _functionInstanceId = functionInstanceId;
            _functionCancellationToken = functionCancellationToken;
            _trace = trace;
        }

        /// <summary>
        /// Gets the instance ID of the function being bound to.
        /// </summary>
        public Guid FunctionInstanceId
        {
            get { return _functionInstanceId; }
        }

        /// <summary>
        /// Gets the <see cref="CancellationToken"/> to use.
        /// </summary>
        public CancellationToken FunctionCancellationToken
        {
            get { return _functionCancellationToken; }
        }

        /// <summary>
        /// Gets the output <see cref="TraceWriter"/>.
        /// </summary>
        public TraceWriter Trace
        {
            get { return _trace; }
        }
    }
}
