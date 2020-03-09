// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Interface for user text element
    /// </summary>
    public interface IUserTextElement
    {
        /// <summary>
        /// Gets the summary.
        /// </summary>
        string Summary { get; }

        /// <summary>
        /// Gets the description.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the tooltip.
        /// </summary>
        string Tooltip { get; }
    }
}