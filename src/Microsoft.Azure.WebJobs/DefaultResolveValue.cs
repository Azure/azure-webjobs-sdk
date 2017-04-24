// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Description
{
    /// <summary>
    /// If set, 
    /// </summary>
    public enum DefaultResolveValue
    {
        /// <summary>
        /// Don't default 
        /// </summary>
        None,

        /// <summary>
        /// Default to this function name. 
        /// This may use the <see cref="FunctionNameAttribute"/> 
        /// </summary>
        MemberName
    }
}