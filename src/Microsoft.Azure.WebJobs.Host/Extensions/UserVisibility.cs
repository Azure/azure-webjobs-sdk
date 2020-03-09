// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host
{
    public enum UserVisibility
    {
        /// <summary>
        /// The user visibility is not specified.
        /// </summary>
        NotSpecified,

        /// <summary>
        /// The user visibility is important (visible by default).
        /// </summary>
        Important,

        /// <summary>
        /// The user visibility is advanced (visible once expanded).
        /// </summary>
        Advanced,

        /// <summary>
        /// The user visibility is internal (not visible to user).
        /// </summary>
        Internal,
    }
}