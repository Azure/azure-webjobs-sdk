// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // The core object we get when an ServiceBus is triggered. 
    // This gets converted to the user type (Message, string, poco, etc) 
    internal sealed class ServiceBusTriggerInput
    {
        // If != -1, then only process a single message in this batch. 
        private int _selector = -1;

        public MessageReceiver MessageReceiver { get; set; }
        public IMessageSession MessageSession { get; set; }

        internal Message[] Messages { get; set; }

        public bool IsSingleDispatch
        {
            get
            {
                return _selector != -1;
            }
        }

        public static ServiceBusTriggerInput New(Message message)
        {
            return new ServiceBusTriggerInput
            {
                Messages = new Message[]
                {
                      message
                },
                _selector = 0,
            };
        }

        public ServiceBusTriggerInput GetSingleEventTriggerInput(int idx)
        {
            return new ServiceBusTriggerInput
            {
                Messages = this.Messages,
                MessageReceiver = this.MessageReceiver,
                MessageSession = this.MessageSession,
                _selector = idx
            };
        }

        public Message GetSingleMessage()
        {
            return this.Messages[this._selector];
        }
    }
}