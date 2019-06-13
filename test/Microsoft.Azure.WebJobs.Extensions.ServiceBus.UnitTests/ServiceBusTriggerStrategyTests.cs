// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using static Microsoft.Azure.ServiceBus.Message;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests
{
    public class ServiceBusTriggerStrategyTests
    {
        [Fact]
        public void GetStaticBindingContract_ReturnsExpectedValue()
        {
            var strategy = new ServiceBusTriggerBindingStrategy();
            var bindingDataContract = strategy.GetBindingContract();

            CheckBindingContract(bindingDataContract);
        }

        [Fact]
        public void GetBindingContract_SingleDispatch_ReturnsExpectedValue()
        {
            var strategy = new ServiceBusTriggerBindingStrategy();
            var bindingDataContract = strategy.GetBindingContract(true);

            CheckBindingContract(bindingDataContract);
        }

        [Fact]
        public void GetBindingContract_MultipleDispatch_ReturnsExpectedValue()
        {
            var strategy = new ServiceBusTriggerBindingStrategy();
            var bindingDataContract = strategy.GetBindingContract(false);

            Assert.Equal(15, bindingDataContract.Count);
            Assert.Equal(bindingDataContract["DeliveryCountArray"], typeof(int[]));
            Assert.Equal(bindingDataContract["DeadLetterSourceArray"], typeof(string[]));
            Assert.Equal(bindingDataContract["LockTokenArray"], typeof(string[]));
            Assert.Equal(bindingDataContract["ExpiresAtUtcArray"], typeof(DateTime[]));
            Assert.Equal(bindingDataContract["EnqueuedTimeUtcArray"], typeof(DateTime[]));
            Assert.Equal(bindingDataContract["MessageIdArray"], typeof(string[]));
            Assert.Equal(bindingDataContract["ContentTypeArray"], typeof(string[]));
            Assert.Equal(bindingDataContract["ReplyToArray"], typeof(string[]));
            Assert.Equal(bindingDataContract["SequenceNumberArray"], typeof(long[]));
            Assert.Equal(bindingDataContract["ToArray"], typeof(string[]));
            Assert.Equal(bindingDataContract["LabelArray"], typeof(string[]));
            Assert.Equal(bindingDataContract["CorrelationIdArray"], typeof(string[]));
            Assert.Equal(bindingDataContract["UserPropertiesArray"], typeof(IDictionary<string, object>[]));
            Assert.Equal(bindingDataContract["MessageReceiver"], typeof(MessageReceiver));
            Assert.Equal(bindingDataContract["MessageSession"], typeof(IMessageSession));
        }

        [Fact]
        public void GetBindingData_SingleDispatch_ReturnsExpectedValue()
        {
            var message = new Message(new byte[] { });
            SystemPropertiesCollection sysProp = GetSystemProperties();
            TestHelpers.SetField(message, "SystemProperties", sysProp);
            IDictionary<string, object> userProps = new Dictionary<string, object>();
            userProps.Add(new KeyValuePair<string, object>("prop1", "value1"));
            userProps.Add(new KeyValuePair<string, object>("prop2", "value2"));
            TestHelpers.SetField(message, "UserProperties", userProps);

            var input = ServiceBusTriggerInput.New(message);
            var strategy = new ServiceBusTriggerBindingStrategy();
            var bindingData = strategy.GetBindingData(input);

            Assert.Equal(15, bindingData.Count);  // SystemPropertiesCollection is sealed 

            Assert.Same(input.MessageReceiver, bindingData["MessageReceiver"]);
            Assert.Same(input.MessageSession, bindingData["MessageSession"]);
            Assert.Equal(message.SystemProperties.LockToken, bindingData["LockToken"]);
            Assert.Equal(message.SystemProperties.SequenceNumber, bindingData["SequenceNumber"]);
            Assert.Equal(message.SystemProperties.DeliveryCount, bindingData["DeliveryCount"]);
            Assert.Same(message.SystemProperties.DeadLetterSource, bindingData["DeadLetterSource"]);
            Assert.Equal(message.ExpiresAtUtc, bindingData["ExpiresAtUtc"]);
            Assert.Same(message.MessageId, bindingData["MessageId"]);
            Assert.Same(message.ContentType, bindingData["ContentType"]);
            Assert.Same(message.ReplyTo, bindingData["ReplyTo"]);
            Assert.Same(message.To, bindingData["To"]);
            Assert.Same(message.Label, bindingData["Label"]);
            Assert.Same(message.CorrelationId, bindingData["CorrelationId"]);

            IDictionary<string, object> bindingDataUserProps = bindingData["UserProperties"] as Dictionary<string, object>;
            Assert.NotNull(bindingDataUserProps);
            Assert.Equal(bindingDataUserProps["prop1"], "value1");
            Assert.Equal(bindingDataUserProps["prop2"], "value2");
        }

        [Fact]
        public void GetBindingData_MultipleDispatch_ReturnsExpectedValue()
        {

            var messages = new Message[3]
            {
                new Message(Encoding.UTF8.GetBytes("Event 1")),
                new Message(Encoding.UTF8.GetBytes("Event 2")),
                new Message(Encoding.UTF8.GetBytes("Event 3")),
            };

            foreach (var message in messages)
            {
                SystemPropertiesCollection sysProps = GetSystemProperties();
                TestHelpers.SetField(message, "SystemProperties", sysProps);
            }

            var input = new ServiceBusTriggerInput
            {
                Messages = messages
            };
            var strategy = new ServiceBusTriggerBindingStrategy();
            var bindingData = strategy.GetBindingData(input);

            Assert.Equal(15, bindingData.Count);
            Assert.Same(input.MessageReceiver, bindingData["MessageReceiver"]);
            Assert.Same(input.MessageSession, bindingData["MessageSession"]);

            // verify an array was created for each binding data type
            Assert.Equal(messages.Length, ((int[])bindingData["DeliveryCountArray"]).Length);
            Assert.Equal(messages.Length, ((string[])bindingData["DeadLetterSourceArray"]).Length);
            Assert.Equal(messages.Length, ((string[])bindingData["LockTokenArray"]).Length);
            Assert.Equal(messages.Length, ((DateTime[])bindingData["ExpiresAtUtcArray"]).Length);
            Assert.Equal(messages.Length, ((DateTime[])bindingData["EnqueuedTimeUtcArray"]).Length);
            Assert.Equal(messages.Length, ((string[])bindingData["MessageIdArray"]).Length);
            Assert.Equal(messages.Length, ((string[])bindingData["ContentTypeArray"]).Length);
            Assert.Equal(messages.Length, ((string[])bindingData["ReplyToArray"]).Length);
            Assert.Equal(messages.Length, ((long[])bindingData["SequenceNumberArray"]).Length);
            Assert.Equal(messages.Length, ((string[])bindingData["ToArray"]).Length);
            Assert.Equal(messages.Length, ((string[])bindingData["LabelArray"]).Length);
            Assert.Equal(messages.Length, ((string[])bindingData["CorrelationIdArray"]).Length);
            Assert.Equal(messages.Length, ((IDictionary<string, object>[])bindingData["UserPropertiesArray"]).Length);
        }

        private static void CheckBindingContract(Dictionary<string, Type> bindingDataContract)
        {
            Assert.Equal(15, bindingDataContract.Count);
            Assert.Equal(bindingDataContract["DeliveryCount"], typeof(int));
            Assert.Equal(bindingDataContract["DeadLetterSource"], typeof(string));
            Assert.Equal(bindingDataContract["LockToken"], typeof(string));
            Assert.Equal(bindingDataContract["ExpiresAtUtc"], typeof(DateTime));
            Assert.Equal(bindingDataContract["EnqueuedTimeUtc"], typeof(DateTime));
            Assert.Equal(bindingDataContract["MessageId"], typeof(string));
            Assert.Equal(bindingDataContract["ContentType"], typeof(string));
            Assert.Equal(bindingDataContract["ReplyTo"], typeof(string));
            Assert.Equal(bindingDataContract["SequenceNumber"], typeof(long));
            Assert.Equal(bindingDataContract["To"], typeof(string));
            Assert.Equal(bindingDataContract["Label"], typeof(string));
            Assert.Equal(bindingDataContract["CorrelationId"], typeof(string));
            Assert.Equal(bindingDataContract["UserProperties"], typeof(IDictionary<string, object>));
            Assert.Equal(bindingDataContract["MessageReceiver"], typeof(MessageReceiver));
            Assert.Equal(bindingDataContract["MessageSession"], typeof(IMessageSession));
        }

        private static SystemPropertiesCollection GetSystemProperties()
        {
            SystemPropertiesCollection sysProps = new SystemPropertiesCollection();
            TestHelpers.SetField(sysProps, "deliveryCount", 1);
            TestHelpers.SetField(sysProps, "lockedUntilUtc", DateTime.MinValue);
            TestHelpers.SetField(sysProps, "sequenceNumber", 1);
            TestHelpers.SetField(sysProps, "enqueuedTimeUtc", DateTime.MinValue);
            TestHelpers.SetField(sysProps, "lockTokenGuid", Guid.NewGuid());
            TestHelpers.SetField(sysProps, "deadLetterSource", "test");
            return sysProps;
        }

        [Fact]
        public void TriggerStrategy()
        {
            string data = "123";

            var strategy = new ServiceBusTriggerBindingStrategy();
            ServiceBusTriggerInput triggerInput = strategy.ConvertFromString(data);

            var contract = strategy.GetBindingData(triggerInput);

            Message single = strategy.BindSingle(triggerInput, null);
            string body = Encoding.UTF8.GetString(single.Body);

            Assert.Equal(data, body);
            Assert.Null(contract["MessageReceiver"]);
            Assert.Null(contract["MessageSession"]);
        }
    }
}
