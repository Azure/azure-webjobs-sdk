﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Blobs;
using Microsoft.Azure.WebJobs.Host.Queues;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    public class FunctionBindingContext
    {
        private readonly Guid _functionInstanceId;
        private readonly CancellationToken _functionCancellationToken;
        private readonly TextWriter _consoleOutput;

        public FunctionBindingContext(Guid functionInstanceId, CancellationToken functionCancellationToken,
            TextWriter consoleOutput)
        {
            _functionInstanceId = functionInstanceId;
            _functionCancellationToken = functionCancellationToken;
            _consoleOutput = consoleOutput;
        }

        public Guid FunctionInstanceId
        {
            get { return _functionInstanceId; }
        }

        public CancellationToken FunctionCancellationToken
        {
            get { return _functionCancellationToken; }
        }

        public TextWriter ConsoleOutput
        {
            get { return _consoleOutput; }
        }
    }
}
