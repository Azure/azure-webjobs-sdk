// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class FastTableLoggerProviderTests
    {
        [Fact]
        public void Output_IsSynchronized()
        {
            IFunctionOutputLogger provider = new FastTableLoggerProvider(new TestTraceWriter(TraceLevel.Verbose));
            var instanceMock = new Mock<IFunctionInstance>();
            var outputDef = provider.CreateAsync(instanceMock.Object, CancellationToken.None).Result;
            var output = outputDef.CreateOutput();

            ExceptionDispatchInfo exception = null;

            List<Thread> threads = new List<Thread>();
            for (int i = 0; i < 5; i++)
            {
                var t = new Thread(() =>
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        try
                        {
                            output.Output.WriteLine($"{i};{j}");
                        }
                        catch (Exception ex)
                        {
                            exception = ExceptionDispatchInfo.Capture(ex);
                            throw;
                        }
                    }
                });

                threads.Add(t);
            }

            foreach (var t in threads)
            {
                t.Start();
            }

            foreach (var t in threads)
            {
                t.Join();
            }

            if (exception != null)
            {
                exception.Throw();
            }
        }
    }
}
