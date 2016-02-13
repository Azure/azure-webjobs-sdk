// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.ServiceBus.Messaging;
using Microsoft.Azure.WebJobs.Host.Protocols;
using System.Globalization;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Executors;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // Strategy pattern to describe how to bind a core trigger type to various parameter.
    // Suports:
    // - both Single-item vs. Batch dispatch.  
    // - core binding, string, poco w/ Binding Contracts
    // 
    // For example, a single EventHubTriggerInput -->  can bind to 
    //  EventData, EventData[], string, string[], Poco, Poco[]    
    interface ITriggerBindingStrategy<TMessage, TTriggerValue>
    {
        string ConvertEventData2String(TMessage x);

        TTriggerValue ConvertFromString(string x);

        // Intentionally make this mutable so that callers can add more items to it. 
        Dictionary<string, Type> GetCoreContract();

        // Intentionally make this mutable so that callers can add more items to it. 
        Dictionary<string, object> GetContractInstance(TTriggerValue value);

        // The most basic binding
        TMessage BindMessage(TTriggerValue value, ValueBindingContext context);

        TMessage[] BindMessageArray(TTriggerValue value, ValueBindingContext context);
    }
}