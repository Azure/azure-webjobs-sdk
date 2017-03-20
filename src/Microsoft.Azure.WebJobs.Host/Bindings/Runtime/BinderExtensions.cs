// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Provides extension methods for <see cref="Binder"/>.
    /// </summary>
    public static class BinderExtensions
    {
        /// <summary>
        /// Binds the specified attribute.
        /// </summary>
        /// <typeparam name="T">The type to which to bind.</typeparam>
        /// <param name="binder">The binder to use to bind.</param>
        /// <param name="attributes">The collection of attributes to bind. The first attribute in the
        /// collection should be the primary attribute.</param>
        /// <returns></returns>
        public static T Bind<T>(this Binder binder, Attribute[] attributes)
        {
            if (binder == null)
            {
                throw new ArgumentNullException(nameof(binder));
            }

            return binder.BindAsync<T>(attributes).GetAwaiter().GetResult();
        }
    }
}