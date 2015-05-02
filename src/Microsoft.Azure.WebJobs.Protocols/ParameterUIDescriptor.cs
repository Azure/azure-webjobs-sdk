// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json;

#if PUBLICPROTOCOL
namespace Microsoft.Azure.WebJobs.Protocols
#else
namespace Microsoft.Azure.WebJobs.Host.Protocols
#endif
{
    /// <summary>Provides UI details for a parameter to an Azure WebJobs SDK function.</summary>
    public class ParameterUIDescriptor
    {
        /// <summary>Gets or sets the description.</summary>
        public virtual string Description { get; set; }

        /// <summary>Gets or sets the attribute text.</summary>
        public virtual string AttributeText { get; set; }

        /// <summary>Gets or sets the ui prompt.</summary>
        public virtual string Prompt { get; set; }

        /// <summary>Gets or sets the default value.</summary>
        public virtual string DefaultValue { get; set; }
    }
}
