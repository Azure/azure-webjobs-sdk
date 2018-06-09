﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage.Queue;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    public class QueueTests
    {
        private const string TriggerQueueName = "input";
        private const string QueueName = "output";

        // Test binding to generics. 
        public class GenericProgram<T>
        {
            public void Func([Queue(QueueName)] T q)
            {
                var x = (ICollector<string>)q;
                x.Add("123");
            }
        }

        [Fact]
        public async Task TestGenericSucceeds()
        {
            var account = CreateFakeStorageAccount();
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<GenericProgram<ICollector<string>>>()
                .UseStorage(account)
                .Build();

            host.GetJobHost().Call<GenericProgram<ICollector<string>>>("Func");

            // Now peek at messages. 
            var queue = account.CreateCloudQueueClient().GetQueueReference(QueueName);
            var msgs = (await queue.GetMessagesAsync(10)).ToArray();

            Assert.Single(msgs);
            Assert.Equal("123", msgs[0].AsString);
        }

        // Program with a static bad queue name (no { } ). 
        // Use this to test queue name validation. 
        public class ProgramWithStaticBadName
        {
            public const string BadQueueName = "test*"; // Don't include any { }

            // Queue paths without any { } are eagerly validated at indexing time.
            public void Func([Queue(BadQueueName)] ICollector<string> q)
            {
            }
        }

        [Fact]
        public void Catch_Bad_Name_At_IndexTime()
        {
            IHost host = new HostBuilder()
               .ConfigureDefaultTestHost<ProgramWithStaticBadName>()
               .Build();

            string errorMessage = GetErrorMessageForBadQueueName(ProgramWithStaticBadName.BadQueueName, "name");

            TestHelpers.AssertIndexingError(() => host.GetJobHost().Call<ProgramWithStaticBadName>("Func"), "ProgramWithStaticBadName.Func", errorMessage);
        }

        private static string GetErrorMessageForBadQueueName(string value, string parameterName)
        {
            return "A queue name can contain only letters, numbers, and and dash(-) characters - \"" + value + "\"" +
                "\r\nParameter name: " + parameterName; // from ArgumentException 
        }

        // Program with variable queue name containing both %% and { }.
        // Has valid parameter binding.   Use this to test queue name validation at various stages. 
        public class ProgramWithVariableQueueName
        {
            public const string QueueNamePattern = "q%key%-test{x}";

            // Queue paths without any { } are eagerly validated at indexing time.
            public void Func([Queue(QueueNamePattern)] ICollector<string> q)
            {
            }
        }

        [Fact]
        public void Catch_Bad_Name_At_Runtime()
        {
            var nameResolver = new FakeNameResolver().Add("key", "1");
            IHost host = new HostBuilder()
               .ConfigureDefaultTestHost<ProgramWithVariableQueueName>()
               .UseFakeStorage()
               .ConfigureServices(services =>
               {
                   services.AddSingleton<INameResolver>(nameResolver);
               })
               .Build();

            host.GetJobHost().Call<ProgramWithVariableQueueName>("Func", new { x = "1" }); // succeeds with valid char

            try
            {
                host.GetJobHost().Call<ProgramWithVariableQueueName>("Func", new { x = "*" }); // produces an error pattern. 
                Assert.False(true, "should have failed");
            }
            catch (FunctionInvocationException e)
            {
                Assert.Equal("Exception binding parameter 'q'", e.InnerException.Message);

                string errorMessage = GetErrorMessageForBadQueueName("q1-test*", "name");
                Assert.Equal(errorMessage, e.InnerException.InnerException.Message);
            }
        }

        // The presence of { } defers validation until runtime. Even if there are illegal chars known at index time! 
        [Fact]
        public void Catch_Bad_Name_At_Runtime_With_Illegal_Static_Chars()
        {
            var nameResolver = new FakeNameResolver().Add("key", "$"); // Illegal

            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ProgramWithVariableQueueName>()
                .UseFakeStorage()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<INameResolver>(nameResolver);
                })
                .Build();
            try
            {
                host.GetJobHost().Call<ProgramWithVariableQueueName>("Func", new { x = "1" }); // produces an error pattern. 
                Assert.False(true, "should have failed");
            }
            catch (FunctionInvocationException e) // Not an index exception!
            {
                Assert.Equal("Exception binding parameter 'q'", e.InnerException.Message);

                string errorMessage = GetErrorMessageForBadQueueName("q$-test1", "name");
                Assert.Equal(errorMessage, e.InnerException.InnerException.Message);
            }
        }

        public class ProgramWithTriggerAndBindingData
        {
            public class Poco
            {
                public string xyz { get; set; }
            }

            // BindingData is case insensitive. 
            // And queue name is normalized to lowercase. 
            // Connection="" is same as Connection=null
            public const string QueueOutName = "qName-{XYZ}";
            public void Func([QueueTrigger(QueueName, Connection = "")] Poco triggers, [Queue(QueueOutName)] ICollector<string> q)
            {
                q.Add("123");
            }
        }

        [Fact]
        public async Task InvokeWithBindingData()
        {
            // Verify that queue binding pattern has uppercase letters in it. These get normalized to lowercase.
            Assert.NotEqual(ProgramWithTriggerAndBindingData.QueueOutName, ProgramWithTriggerAndBindingData.QueueOutName.ToLower());

            var account = CreateFakeStorageAccount();
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ProgramWithTriggerAndBindingData>()
                .UseFakeStorage()
                .Build();

            var trigger = new ProgramWithTriggerAndBindingData.Poco { xyz = "abc" };
            host.GetJobHost().Call<ProgramWithTriggerAndBindingData>("Func", new
            {
                triggers = new CloudQueueMessage(JsonConvert.SerializeObject(trigger))
            });

            // Now peek at messages. 
            // queue name is normalized to lowercase. 
            var queue = account.CreateCloudQueueClient().GetQueueReference("qname-abc");
            var msgs = (await queue.GetMessagesAsync(10)).ToArray();

            Assert.Single(msgs);
            Assert.Equal("123", msgs[0].AsString);
        }

        public class ProgramWithTriggerAndCompoundBindingData
        {
            public class Poco
            {
                public SubOject prop1 { get; set; }
                public string xyz { get; set; }
            }

            public class SubOject
            {
                public string xyz { get; set; }
            }

            // BindingData is case insensitive. 
            // And queue name is normalized to lowercase. 
            public const string QueueOutName = "qName-{prop1.xyz}";
            public void Func(
                [QueueTrigger(QueueName)] Poco triggers,
                [Queue(QueueOutName)] ICollector<string> q,
                string xyz, // {xyz}
                SubOject prop1) // Bind to a object 
            {
                // binding to subobject work 
                Assert.NotNull(prop1);
                Assert.Equal(prop1.xyz, "abc");

                Assert.Equal("bad", xyz);

                q.Add("123");
            }
        }

        [Fact]
        public async Task InvokeWithCompoundBindingData()
        {
            // Verify that queue binding pattern has uppercase letters in it. These get normalized to lowercase.
            Assert.NotEqual(ProgramWithTriggerAndBindingData.QueueOutName, ProgramWithTriggerAndBindingData.QueueOutName.ToLower());

            var account = CreateFakeStorageAccount();
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ProgramWithTriggerAndCompoundBindingData>()
                .UseStorage(account)
                .Build();

            var trigger = new ProgramWithTriggerAndCompoundBindingData.Poco
            {
                xyz = "bad",
                prop1 = new ProgramWithTriggerAndCompoundBindingData.SubOject
                {
                    xyz = "abc"
                }
            };

            host.GetJobHost().Call<ProgramWithTriggerAndCompoundBindingData>("Func", new
            {
                triggers = new CloudQueueMessage(JsonConvert.SerializeObject(trigger))
            });

            // Now peek at messages. 
            // queue name is normalized to lowercase. 
            var queue = account.CreateCloudQueueClient().GetQueueReference("qname-abc");
            var msgs = (await queue.GetMessagesAsync(10)).ToArray();

            Assert.Single(msgs);
            Assert.Equal("123", msgs[0].AsString);
        }

        public class ProgramSimple
        {
            public void Func([Queue(QueueName)] out string x)
            {
                x = "abc";
            }
        }

        public class ProgramSimple2
        {
            public const string ConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test";

            // Validate that we sanitize the error to remove a connection string if someone accidentally uses one.
            public void Func2([Queue(QueueName, Connection = ConnectionString)] out string x)
            {
                x = "abc";
            }
        }

#if false // $$$ enable 
        // Nice failure when no storage account is set
        [Fact]
        public void Fails_When_No_Storage_is_set()
        {
            // TODO: We shouldn't have to do this, but our default parser
            //       does not allow for null Storage/Dashboard.
            var mockParser = new Mock<IStorageAccountParser>();
            mockParser
                .Setup(p => p.ParseAccount(null, It.IsAny<string>()))
                .Returns<string>(null);

            // no storage account!
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ProgramSimple>()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IStorageAccountParser>(mockParser.Object);
                })
                .ConfigureAppConfiguration(config =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "AzureWebJobsStorge", null },
                        { "AzureWebJobsDashboard", null }
                    });
                })
                .Build();

            string message = StorageAccountParser.FormatParseAccountErrorMessage(StorageAccountParseResult.MissingOrEmptyConnectionStringError, "Storage");
            TestHelpers.AssertIndexingError(() => host.GetJobHost().Call<ProgramSimple>("Func"), "ProgramSimple.Func", message);
        }

        [Fact]
        public void Sanitizes_Exception_If_Connection_String()
        {
            // people accidentally use their connection string; we want to make sure we sanitize it
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ProgramSimple2>()
                .Build();

            string message = StorageAccountParser.FormatParseAccountErrorMessage(StorageAccountParseResult.MissingOrEmptyConnectionStringError, ProgramSimple2.ConnectionString);

            TestHelpers.AssertIndexingError(() => host.GetJobHost().Call<ProgramSimple2>(nameof(ProgramSimple2.Func2)),
                "ProgramSimple2.Func2", message);

            Assert.DoesNotContain(ProgramSimple2.ConnectionString, message);
            Assert.DoesNotContain("AzureWebJobs", message); // prefix should not be added
            Assert.Contains("[Hidden Credential]", message);
        }
#endif 

        public class ProgramBadContract
        {
            public void Func([QueueTrigger(QueueName)] string triggers, [Queue("queuName-{xyz}")] ICollector<string> q)
            {
            }
        }

        [Fact]
        public void Fails_BindingContract_Mismatch()
        {
            // Verify that indexing fails if the [Queue] trigger needs binding data that's not present. 
            var account = CreateFakeStorageAccount();
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ProgramBadContract>()
                .UseStorage(account)
                .Build();

            TestHelpers.AssertIndexingError(() => host.GetJobHost().Call<ProgramBadContract>("Func"),
                "ProgramBadContract.Func",
                string.Format(CultureInfo.CurrentCulture, Constants.UnableToResolveBindingParameterFormat, "xyz"));
        }

        public class ProgramCantBindToObject
        {
            public void Func([Queue(QueueName)] out object o)
            {
                o = null;
            }
        }

        [Fact]
        public void Fails_Cant_Bind_To_Object()
        {
            var account = CreateFakeStorageAccount();
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ProgramCantBindToObject>()
                .UseStorage(account)
                .Build();

            TestHelpers.AssertIndexingError(() => host.GetJobHost().Call<ProgramCantBindToObject>("Func"),
                "ProgramCantBindToObject.Func",
                "Object element types are not supported.");
        }

        [Theory]
        [InlineData(typeof(int), "System.Int32")]
        [InlineData(typeof(DateTime), "System.DateTime")]
        [InlineData(typeof(IEnumerable<string>), "System.Collections.Generic.IEnumerable`1[System.String]")] // Should use ICollector<string> instead
        public void Fails_Cant_Bind_To_Types(Type typeParam, string typeName)
        {
            var m = this.GetType().GetMethod("Fails_Cant_Bind_To_Types_Worker", BindingFlags.Instance | BindingFlags.NonPublic);
            var m2 = m.MakeGenericMethod(typeParam);
            try
            {
                m2.Invoke(this, new object[] { typeName });
            }
            catch (TargetException e)
            {
                throw e.InnerException;
            }
        }

        private void Fails_Cant_Bind_To_Types_Worker<T>(string typeName)
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<GenericProgram<T>>()
                .UseFakeStorage()                
                .Build();

            TestHelpers.AssertIndexingError(() => host.GetJobHost().Call<GenericProgram<T>>("Func"),
                "GenericProgram`1.Func",
                "Can't bind Queue to type '" + typeName + "'.");
        }

        [Fact]
        public async Task Queue_IfBoundToCloudQueue_BindsAndCreatesQueue()
        {
            // Arrange
            var account = CreateFakeStorageAccount();
            CloudQueueClient client = account.CreateCloudQueueClient();
            CloudQueue triggerQueue = await CreateQueue(client, TriggerQueueName);
            await triggerQueue.AddMessageAsync(new CloudQueueMessage("ignore"));

            // Act
            CloudQueue result = RunTrigger<CloudQueue>(account, typeof(BindToCloudQueueProgram),
                (s) => BindToCloudQueueProgram.TaskSource = s);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(QueueName, result.Name);
            var queue = client.GetQueueReference(QueueName);
            Assert.True(await queue.ExistsAsync());
        }

        [Fact]
        public async Task Queue_IfBoundToICollectorCloudQueueMessage_AddEnqueuesMessage()
        {
            // Arrange
            string expectedContent = Guid.NewGuid().ToString();
            var account = CreateFakeStorageAccount();
            var client = account.CreateCloudQueueClient();
            var triggerQueue = await CreateQueue(client, TriggerQueueName);
            await triggerQueue.AddMessageAsync(new CloudQueueMessage(expectedContent));

            // Act
            RunTrigger<object>(account, typeof(BindToICollectorCloudQueueMessageProgram),
                (s) => BindToICollectorCloudQueueMessageProgram.TaskSource = s);

            // Assert
            var queue = client.GetQueueReference(QueueName);
            IEnumerable<CloudQueueMessage> messages = await queue.GetMessagesAsync(messageCount: 10);
            Assert.NotNull(messages);
            Assert.Single(messages);
            CloudQueueMessage message = messages.Single();
            Assert.Equal(expectedContent, message.AsString);
        }

        private static XStorageAccount CreateFakeStorageAccount()
        {
            return new XFakeStorageAccount();
        }

        private static async Task<CloudQueue> CreateQueue(CloudQueueClient client, string queueName)
        {
            var queue = client.GetQueueReference(queueName);
            await queue.CreateIfNotExistsAsync();
            return queue;
        }

        private static TResult RunTrigger<TResult>(XStorageAccount account, Type programType,
            Action<TaskCompletionSource<TResult>> setTaskSource)
        {
            return FunctionalTest.RunTrigger<TResult>(account, programType, setTaskSource);
        }

        private class BindToCloudQueueProgram
        {
            public static TaskCompletionSource<CloudQueue> TaskSource { get; set; }

            public static void Run([QueueTrigger(TriggerQueueName)] CloudQueueMessage ignore,
                [Queue(QueueName)] CloudQueue queue)
            {
                TaskSource.TrySetResult(queue);
            }
        }

        private class BindToICollectorCloudQueueMessageProgram
        {
            public static TaskCompletionSource<object> TaskSource { get; set; }

            public static void Run([QueueTrigger(TriggerQueueName)] CloudQueueMessage message,
                [Queue(QueueName)] ICollector<CloudQueueMessage> queue)
            {
                queue.Add(message);
                TaskSource.TrySetResult(null);
            }
        }
    }
}
