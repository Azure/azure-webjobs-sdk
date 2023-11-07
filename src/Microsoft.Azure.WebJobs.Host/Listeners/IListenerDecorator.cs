// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Listeners
{
    /// <summary>
    /// Custom decorator interface called during <see cref="JobHost"/> listener creation to
    /// allow function listeners to be customized.
    /// </summary>
    public interface IListenerDecorator
    {
        /// <summary>
        /// Creates a listener.
        /// </summary>
        /// <param name="context">The listener context.</param>
        /// <returns>The listener to use. This may be a new wrapped listener, or the original
        /// listener.</returns>
        IListener Decorate(ListenerDecoratorContext context);
    }
}