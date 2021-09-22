// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Indexers
{
    public class FunctionIndexerIntegrationTests
    {
        // Helper to do the indexing.
        private static Tuple<FunctionDescriptor, IFunctionDefinition> IndexMethod(string methodName, INameResolver nameResolver = null)
        {
            MethodInfo method = typeof(FunctionIndexerIntegrationTests).GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method);

            FunctionIndexer indexer = FunctionIndexerFactory.Create(nameResolver);

            Tuple<FunctionDescriptor, IFunctionDefinition> indexEntry = null;
            Mock<IFunctionIndexCollector> indexMock = new Mock<IFunctionIndexCollector>(MockBehavior.Strict);
            indexMock
                .Setup((i) => i.Add(
                    It.IsAny<IFunctionDefinition>(),
                    It.IsAny<FunctionDescriptor>(),
                    It.IsAny<MethodInfo>()))
                .Callback<IFunctionDefinition, FunctionDescriptor, MethodInfo>(
                    (ifd, fd, i) => indexEntry = Tuple.Create(fd, ifd));
            IFunctionIndexCollector index = indexMock.Object;

            indexer.IndexMethodAsync(method, index, CancellationToken.None).GetAwaiter().GetResult();

            return indexEntry;
        }

        private static void NoAutoTrigger1([Blob(@"daas-test-input/{name}.csv")] TextReader inputs) { }

        [Fact]
        public void TestNoAutoTrigger1()
        {
            var entry = IndexMethod("NoAutoTrigger1");

            Assert.NotNull(entry);

            IFunctionDefinition definiton = entry.Item2;
            Assert.NotNull(definiton);
            Assert.Null(definiton.ListenerFactory);

            FunctionDescriptor descriptor = entry.Item1;
            Assert.NotNull(descriptor);
            var parameters = descriptor.Parameters;
            Assert.Single(parameters);
        }

        private static void NameResolver([Blob(@"input/%name%")] TextReader inputs) { }

        [Fact]
        public void TestNameResolver()
        {
            var nameResolver = new FakeNameResolver();
            nameResolver.Add("name", "VALUE");

            FunctionDescriptor func = IndexMethod("NameResolver", nameResolver).Item1;

            Assert.NotNull(func);
            var parameters = func.Parameters;
            Assert.Single(parameters);
            ParameterDescriptor firstParameter = parameters.First();
            Assert.Equal("inputs", firstParameter.Name);
        }

        public static void AutoTrigger1([BlobTrigger(@"daas-test-input/{name}.csv")] TextReader inputs) { }

        [Fact]
        public void TestAutoTrigger1()
        {
            FunctionDescriptor func = IndexMethod("AutoTrigger1").Item1;

            Assert.NotNull(func);
            var parameters = func.Parameters;
            Assert.Single(parameters);
            Assert.Equal("BlobTriggerParameterDescriptor", parameters.First().GetType().Name);
        }

        [NoAutomaticTrigger]
        public static void NoAutoTrigger2(int x, int y) { }

        [Fact]
        public void TestNoAutoTrigger2()
        {
            var entry = IndexMethod("NoAutoTrigger2");

            Assert.NotNull(entry);

            IFunctionDefinition definiton = entry.Item2;
            Assert.NotNull(definiton);
            Assert.Null(definiton.ListenerFactory);

            FunctionDescriptor descriptor = entry.Item1;
            Assert.NotNull(descriptor);
            var parameters = descriptor.Parameters;
            Assert.Equal(2, parameters.Count());
            Assert.IsType<CallerSuppliedParameterDescriptor>(parameters.ElementAt(0));
            Assert.IsType<CallerSuppliedParameterDescriptor>(parameters.ElementAt(1));
        }

        // Nothing about this method that is indexable.
        // No function (no trigger)
        public static void NoIndex(int x, int y) { }

        [Fact]
        public void TestNoIndex()
        {
            var entry = IndexMethod("NoIndex");

            Assert.Null(entry);
        }

        public static void QueueTrigger([QueueTrigger("inputQueue")] int queueValue) { }

        [Fact]
        public void TestQueueTrigger()
        {
            FunctionDescriptor func = IndexMethod("QueueTrigger").Item1;

            Assert.NotNull(func);
            var parameters = func.Parameters;
            Assert.Single(parameters);

            ParameterDescriptor firstParameter = parameters.First();
            Assert.Equal("QueueTriggerParameterDescriptor", firstParameter.GetType().Name);
            Assert.Equal("queueValue", firstParameter.Name); // parameter name does not.
        }

        // Queue inputs with implicit names.
        public static void QueueOutput([Queue("inputQueue")] out string inputQueue)
        {
            inputQueue = "0";
        }

        [Fact]
        public void TestQueueOutput()
        {
            FunctionDescriptor func = IndexMethod("QueueOutput").Item1;

            Assert.NotNull(func);
            var parameters = func.Parameters;
            Assert.Single(parameters);

            ParameterDescriptor firstParameter = parameters.First();
            Assert.Equal("inputQueue", firstParameter.Name); // parameter name does not.
        }

        // Has an unbound parameter, so this will require an explicit invoke.  
        // Trigger: NoListener, explicit
        [NoAutomaticTrigger]
        public static void HasBlobAndUnboundParameter([BlobTrigger("container")] Stream input, int unbound) { }

        [Fact]
        public void TestHasBlobAndUnboundParameter()
        {
            var entry = IndexMethod("HasBlobAndUnboundParameter");

            Assert.NotNull(entry);
            
            IFunctionDefinition definiton = entry.Item2;
            Assert.NotNull(definiton);
            Assert.Null(definiton.ListenerFactory);

            FunctionDescriptor descriptor = entry.Item1;
            Assert.NotNull(descriptor);
            var parameters = descriptor.Parameters;
            Assert.Equal(2, parameters.Count());

            ParameterDescriptor firstParameter = parameters.ElementAt(0);
            Assert.Equal("input", firstParameter.Name);
            Assert.Equal("BlobTriggerParameterDescriptor", firstParameter.GetType().Name);

            ParameterDescriptor secondParameter = parameters.ElementAt(1);
            Assert.Equal("unbound", secondParameter.Name);
            Assert.IsType<CallerSuppliedParameterDescriptor>(secondParameter);
        }

        // Both parameters are bound. 
        // Trigger: Automatic listener
        public static void HasBlobAndBoundParameter([BlobTrigger(@"container/{bound}")] Stream input, int bound) { }

        [Fact]
        public void TestHasBlobAndBoundParameter()
        {
            FunctionDescriptor func = IndexMethod("HasBlobAndBoundParameter").Item1;

            Assert.NotNull(func);
            var parameters = func.Parameters;
            Assert.Equal(2, parameters.Count());

            ParameterDescriptor firstParameter = parameters.ElementAt(0);
            Assert.Equal("input", firstParameter.Name);
            Assert.Equal("BlobTriggerParameterDescriptor", firstParameter.GetType().Name);

            ParameterDescriptor secondParameter = parameters.ElementAt(1);
            Assert.Equal("bound", secondParameter.Name);
            Assert.IsType<BindingDataParameterDescriptor>(secondParameter);
        }
    }
}
