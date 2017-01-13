// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Class used to perform binding template parameter resolution.
    /// </summary>
    public abstract class ParameterResolver
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        protected ParameterResolver()
        {
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="inner">The inner resolver.</param>
        protected ParameterResolver(ParameterResolver inner)
        {
            if (inner == null)
            {
                throw new ArgumentNullException(nameof(inner));
            }

            InnerResolver = inner;
        }

        /// <summary>
        /// The inner resolver.
        /// </summary>
        protected ParameterResolver InnerResolver { get; private set; }

        /// <summary>
        /// Attempt to resolve the template parameter indicated by the specified
        /// <see cref="ParameterResolverContext"/>.
        /// </summary>
        /// <param name="context">The context for the resolve operation.</param>
        /// <returns>True if the resolution was successful, false otherwise.</returns>
        public abstract bool TryResolve(ParameterResolverContext context);
    }
}
