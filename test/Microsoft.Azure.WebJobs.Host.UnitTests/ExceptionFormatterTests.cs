// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Diagnostics;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    [DotMemoryUnit(CollectAllocations = true)]
    public class ExceptionFormatterTests
    {
        readonly ITestOutputHelper _output;

        public ExceptionFormatterTests(ITestOutputHelper output)
        {
            _output = output;
            DotMemoryUnitTestOutput.SetOutputMethod(output.WriteLine);
        }

        [Fact]
        public void FormatException_RemovesAsyncFrames()
        {
            try
            {
                var test = new TestClass();
                test.Run();
            }
            catch (Exception exc)
            {
                string formattedException = GetFormattedException(exc);

                Assert.Equal(OutputRemovesAsyncFrames, formattedException);

                Assert.DoesNotMatch("d__.\\.MoveNext()", formattedException);
                Assert.DoesNotContain("TaskAwaiter", formattedException);
            }
        }

        [Fact]
        public void FormatException_ResolvesAsyncMethodNames()
        {
            try
            {
                var test = new TestClass();
                test.Run();
            }
            catch (Exception exc)
            {
                string formattedException = GetFormattedException(exc);

                Assert.Equal(OutputResolvesAsyncMethodNames, formattedException);

                string typeName = $"{typeof(TestClass).DeclaringType.FullName}.{ nameof(TestClass)}";
                Assert.Contains($"async {typeName}.{nameof(TestClass.Run1Async)}()", formattedException);
                Assert.Contains($"async {typeName}.{nameof(TestClass.Run2Async)}()", formattedException);
                Assert.Contains($"async {typeName}.{nameof(TestClass.CrashAsync)}()", formattedException);
            }
        }

        [Fact]
        public void FormatException_OutputsMethodParameters()
        {
            try
            {
                var test = new TestClass();
                test.Run();
            }
            catch (Exception exc)
            {
                string formattedException = GetFormattedException(exc);

                Assert.Equal(OutputOutputsMethodParameters, formattedException);


                Assert.Contains($"{nameof(TestClass.Run)}(String arg)", formattedException);
            }
        }

        [Fact]
        public void FormatException_OutputsExpectedAsyncMethodParameters()
        {
            try
            {
                var test = new TestClass();
                test.Run("Test2");
            }
            catch (Exception exc)
            {
                string formattedException = GetFormattedException(exc);

                Assert.Equal(OutputOutputsExpectedAsyncMethodParameters, formattedException);

                Assert.Contains($"{nameof(TestClass.Run4Async)}(String arg)", formattedException);

                // When unable to resolve, the '??' token is used
                
                Assert.Contains($"{nameof(TestClass.Run5Async)}(??)", formattedException);
            }
        }

        string GetFormattedException(Exception exc)
        {
            if (dotMemoryApi.IsEnabled)
            {
                var checkpoint = dotMemory.Check();
                var result = ExceptionFormatter.GetFormattedException(exc);
                long allocated = 0;
                dotMemory.Check(memory => { allocated = memory.GetTrafficFrom(checkpoint).AllocatedMemory.SizeInBytes; });
                _output.WriteLine("Allocated: " + allocated);
                return result;
            }
            else
            {
                return ExceptionFormatter.GetFormattedException(exc);
            }
        }

        private class TestClass
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Run()
            {
                Run("Test1");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Run(string arg)
            {
                if (string.Equals(arg, "Test1"))
                {
                    Run1Async().Wait();
                }
                else if (string.Equals(arg, "Test2"))
                {
                    Run4Async("Arg").Wait();
                }
                else if (string.Equals(arg, "Test3"))
                {
                    try
                    {
                        Run1();
                    }
                    catch (Exception exc)
                    {
                        // Test with inner exception
                        throw new Exception("Crash!", exc);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void Run1()
            {
                throw new Exception("Sync crash!");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public async Task Run1Async()
            {
                await Run2Async();
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public async Task Run2Async()
            {
                await CrashAsync();
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public async Task CrashAsync()
            {
                await Task.Yield();
                throw new Exception("Crash!");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public async Task Run4Async(string arg)
            {
                await Run5Async();
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public async Task Run5Async()
            {
                await CrashAsync();
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public async Task Run5Async(string arg)
            {
                await CrashAsync();
            }
        }

        const string OutputRemovesAsyncFrames =
@"System.AggregateException : One or more errors occurred. (Crash!) ---> Crash!
   at System.Threading.Tasks.Task.ThrowIfExceptional(Boolean includeTaskCanceledExceptions)
   at System.Threading.Tasks.Task.Wait(Int32 millisecondsTimeout,CancellationToken cancellationToken)
   at System.Threading.Tasks.Task.Wait()
   at Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run(String arg) at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 137
   at Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 129
   at Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.FormatException_RemovesAsyncFrames() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 32
---> (Inner Exception #0) System.Exception : Crash!
   at async Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.CrashAsync() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 179
   at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
   at async Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run2Async() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 172
   at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
   at async Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run1Async() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 166<---
";

        const string OutputResolvesAsyncMethodNames =
@"System.AggregateException : One or more errors occurred. (Crash!) ---> Crash!
   at System.Threading.Tasks.Task.ThrowIfExceptional(Boolean includeTaskCanceledExceptions)
   at System.Threading.Tasks.Task.Wait(Int32 millisecondsTimeout,CancellationToken cancellationToken)
   at System.Threading.Tasks.Task.Wait()
   at Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run(String arg) at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 137
   at Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 129
   at Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.FormatException_ResolvesAsyncMethodNames() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 51
---> (Inner Exception #0) System.Exception : Crash!
   at async Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.CrashAsync() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 179
   at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
   at async Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run2Async() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 172
   at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
   at async Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run1Async() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 166<---
"; 
        const string OutputOutputsMethodParameters =
@"System.AggregateException : One or more errors occurred. (Crash!) ---> Crash!
   at System.Threading.Tasks.Task.ThrowIfExceptional(Boolean includeTaskCanceledExceptions)
   at System.Threading.Tasks.Task.Wait(Int32 millisecondsTimeout,CancellationToken cancellationToken)
   at System.Threading.Tasks.Task.Wait()
   at Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run(String arg) at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 137
   at Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 129
   at Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.FormatException_OutputsMethodParameters() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 72
---> (Inner Exception #0) System.Exception : Crash!
   at async Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.CrashAsync() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 179
   at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
   at async Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run2Async() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 172
   at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
   at async Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run1Async() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 166<---
";
        const string OutputOutputsExpectedAsyncMethodParameters =
@"System.AggregateException : One or more errors occurred. (Crash!) ---> Crash!
   at System.Threading.Tasks.Task.ThrowIfExceptional(Boolean includeTaskCanceledExceptions)
   at System.Threading.Tasks.Task.Wait(Int32 millisecondsTimeout,CancellationToken cancellationToken)
   at System.Threading.Tasks.Task.Wait()
   at Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run(String arg) at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 141
   at Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.FormatException_OutputsExpectedAsyncMethodParameters() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 91
---> (Inner Exception #0) System.Exception : Crash!
   at async Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.CrashAsync() at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 179
   at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
   at async Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run5Async(??) at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 191
   at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
   at async Microsoft.Azure.WebJobs.Host.UnitTests.ExceptionFormatterTests.TestClass.Run4Async(String arg) at C:\GIT\azure-webjobs-sdk\test\Microsoft.Azure.WebJobs.Host.UnitTests\ExceptionFormatterTests.cs : 185<---
";
    }
}
