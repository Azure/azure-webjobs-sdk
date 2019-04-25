// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.ServiceBus;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    internal class ServiceBusEntityInfo
    {
        public string QueueName { get; set; }

        public string SubscriptionName { get; set; }

        public string TopicName { get; set; }

        public bool IsSessionsEnabled { get; set; }

        public string EntityPath
        {
            get
            {
                if (!string.IsNullOrEmpty(QueueName))
                {
                    return QueueName;
                }
                else
                {
                    return EntityNameHelper.FormatSubscriptionPath(TopicName, SubscriptionName);
                }
            }
        }
    }
}
