// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.ServiceBus.Triggers
{
    internal class ServiceBusTriggerParameterDescriptor : TriggerParameterDescriptor
    {
        /// <summary>Gets or sets the entity path <see cref="EntityPath"/>.</summary>
        public string EntityPath { get; set; }

        /// <inheritdoc />
        public override string GetTriggerReason(IDictionary<string, string> arguments)
        {
            return string.Format(CultureInfo.CurrentCulture, $"New ServiceBus message detected on '{EntityPath}'.");
        }
    }
}
