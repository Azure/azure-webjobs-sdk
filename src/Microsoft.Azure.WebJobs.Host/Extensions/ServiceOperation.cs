// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json.Schema;

namespace Microsoft.Azure.WebJobs.Host
{
    public class ServiceOperation : IUserTextElement
    {
        /// <summary>
         /// Gets the operation identifier.
         /// </summary>
        public string Id { get; }

        /// <inheritdoc/>
        public string Summary { get; }

        /// <inheritdoc/>
        public string Description { get; }

        /// <inheritdoc/>
        public string Tooltip { get; }

        /// <summary>
        /// Gets the binding identifier.
        /// </summary>
        public string BindingId { get; }

        /// <summary>
        /// Gets the visibility.
        /// </summary>
        public UserVisibility Visibility { get; }

        /// <summary>
        /// Gets the inputs definition
        /// </summary>
        public JSchema InputsDefinition { get; }

        /// <summary>
        /// Gets the responses definition
        /// </summary>
        public JSchema ResponsesDefinition { get; }
    }
}