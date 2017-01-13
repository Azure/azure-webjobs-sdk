// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// The default <see cref="ParameterResolver"/> used by the framework.
    /// </summary>
    public class DefaultParameterResolver : ParameterResolver
    {
        /// <inheritdoc/>
        public override bool TryResolve(ParameterResolverContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            string value;
            BindingParameterResolver resolver = null;
            if (context.BindingData != null && context.BindingData.TryGetValue(context.ParameterName, out value))
            {
                // parameter is resolved from binding data
                context.Value = value;
                return true;
            }
            else if (BindingParameterResolver.TryGetResolver(context.ParameterName, out resolver))
            {
                // parameter maps to one of the built-in system binding
                // parameters (e.g. rand-guid, datetime, etc.)
                context.Value = resolver.Resolve(context.ParameterName);
                return true;
            }

            return false;
        }
    }
}
