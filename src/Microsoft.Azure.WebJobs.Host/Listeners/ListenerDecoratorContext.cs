// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Indexers;
using System;

namespace Microsoft.Azure.WebJobs.Host.Listeners
{
    /// <summary>
    /// Context class for <see cref="IListenerDecorator.Decorate(ListenerDecoratorContext)"/>.
    /// </summary>
    public class ListenerDecoratorContext
    {
        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="functionDefinition">The function the specified listener is for.</param>
        /// <param name="rootListenerType">Gets the type of the root listener.</param>
        /// <param name="listener">The listener to decorate.</param>
        public ListenerDecoratorContext(IFunctionDefinition functionDefinition, Type rootListenerType, IListener listener)
        {
            FunctionDefinition = functionDefinition;
            ListenerType = rootListenerType;
            Listener = listener;
        }

        /// <summary>
        /// Gets the <see cref="IFunctionDefinition"/> the specified listener is for.
        /// </summary>
        public IFunctionDefinition FunctionDefinition { get; }

        /// <summary>
        /// Gets the listener to decorate.
        /// </summary>
        public IListener Listener { get; }

        /// <summary>
        /// Gets the type of the root listener.
        /// </summary>
        public Type ListenerType { get; }
    }
}