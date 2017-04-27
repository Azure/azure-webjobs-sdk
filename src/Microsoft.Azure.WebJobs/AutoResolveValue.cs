// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Description
{
    /// <summary>
    /// Specify the default behavior for an <see cref="AutoResolveAttribute"/> property when it has an empty value. 
    /// These only apply if the property is empty. They don't apply if the property is an expression that evaluates to empty. 
    /// </summary>
    public enum AutoResolveValue
    {
        /// <summary>
        /// Do nothing.
        /// </summary>
        None,

        /// <summary>
        /// Use the method name. 
        /// The method name can be override by the <see cref="FunctionNameAttribute"/> 
        /// </summary>
        MethodName
    }
}