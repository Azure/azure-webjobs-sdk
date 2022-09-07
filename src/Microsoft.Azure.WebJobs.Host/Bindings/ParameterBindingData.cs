// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Script.Abstractions.Description.Binding
{
    public class ParameterBindingData
    {
        /// <summary>
        /// Dictionary for the properties required to hydrate SDK-type objects.
        /// </summary>
        public IDictionary<string, string> Properties = new Dictionary<string, string>();
    }
}
