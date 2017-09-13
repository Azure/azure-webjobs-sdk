// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using System;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Indexers
{
    public class FunctionNameTests
    {
        [Fact]
        public async Task Test()
        {
            var prog = new MyProg();
            var activator = new FakeActivator();
            activator.Add(prog);
            var logger = new MyLogger();
            var host = TestHelpers.NewJobHost<MyProg>(activator, logger);

            // Invoke with method Info
            var method = prog.GetType().GetMethod("Test");
            host.Call(method);
            prog.AssertValid();
            logger.AssertFunctionName(MyProg.NewName);

            // Invoke with new name. 
            await host.CallAsync(MyProg.NewName);
            prog.AssertValid();
            logger.AssertFunctionName(MyProg.NewName);

            // Invoke with original name fails 
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await host.CallAsync("Test"));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await host.CallAsync("MyProg.Test"));
        }

        [Fact]
        public void TestInvalidName()
        {
            var host = TestHelpers.NewJobHost<ProgInvalidName>();
            TestHelpers.AssertIndexingError(() => host.Call("Test"), "ProgInvalidName.Test", "'x y' is not a valid function name.");
        }

        public class ProgInvalidName
        {
            [NoAutomaticTrigger]
            [FunctionName("x y")] // illegal charecters
            public void Test()
            {
            }
        }

        [Fact]
        public async Task TestTemplatedString()
        {
            var prog = new ProgTemplatedName();
            var activator = new FakeActivator();
            activator.Add(prog);
            var logger = new MyLogger();
            var resolver = new DictNameResolver();
            resolver.Add(ProgTemplatedName.NamePlaceholder, ProgTemplatedName.NameValue);
            var host = TestHelpers.NewJobHost<ProgTemplatedName>(activator, logger, resolver);

            // Invoke with new name. 
            await host.CallAsync(ProgTemplatedName.NameValue);
            prog.AssertValid();
            logger.AssertFunctionName(ProgTemplatedName.NameValue);
        }

        public class ProgTemplatedName : ProgTestBase
        {
            public const string NamePlaceholder = "placeholder";
            public const string NameValue = "VALUE";

            public ProgTemplatedName() : base(NameValue) { }

            [NoAutomaticTrigger]
            [FunctionName("%" + NamePlaceholder + "%")]
            public override void Test()
            {
                base.Test();
            }
        }

        public class MyLogger : IAsyncCollector<FunctionInstanceLogEntry>
        {
            public List<string> _functionNames = new List<string>();

            public void AssertFunctionName(string expectedFunctionName)
            {
                Assert.Equal(expectedFunctionName, _functionNames[0]);
                _functionNames.Clear();
            }

            public Task AddAsync(FunctionInstanceLogEntry item, CancellationToken cancellationToken = default(CancellationToken))
            {
                _functionNames.Add(item.FunctionName);
                return Task.CompletedTask;
            }

            public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                return Task.CompletedTask;
            }
        }

        public class MyProg : ProgTestBase
        {
            public const string NewName = "otherName";

            public MyProg() : base(NewName) { }

            [FunctionName(NewName)]
            public override void Test()
            {
                base.Test();
            }
        }

        public abstract class ProgTestBase
        {
            public string Name { get; private set; }
            public int _called;

            public ProgTestBase(string name)
            {
                Name = name;
            }

            public void AssertValid()
            {
                Assert.Equal(1, _called);
                _called = 0;
            }

            [NoAutomaticTrigger]
            public virtual void Test()
            {
                _called++;
            }
        }
    }
}
