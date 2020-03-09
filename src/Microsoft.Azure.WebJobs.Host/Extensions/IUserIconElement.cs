// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Interface for user graphical element with an icon and background branding color
    /// </summary>
    public interface IUserIconElement
    {
        /// <summary>
        /// Gets the icon URI.
        /// </summary>
        Uri IconUri { get; }

        /// <summary>
        /// Gets the brand color in hexadecimal format.
        /// </summary>
        uint BrandColor { get; }
    }
}