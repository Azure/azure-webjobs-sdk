// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Dispatch;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Microsoft.Azure.WebJobs.Host.Executors.JobHostContextFactory;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    // $$$
    // For hoisting the V1 logging work out to Host.Storage.
    internal interface ILegacyLogger
    {
        bool Init(
            IFunctionIndex functions,
            IListenerFactory functionsListenerFactory,
            out IFunctionExecutor hostCallExecutor,
            out IListener listener,
            out HostOutputMessage hostOutputMessage,
            string hostId,
            CancellationToken shutdownToken);
    }

    // $$$ An "empty" implementation of ILegacyLogger that disables everything.
    // V1 WebJobs logging can replace this with a storage-backed impl.
    internal class DisableLegacyLogger : ILegacyLogger
    {
        private readonly IFunctionExecutor _functionExecutor;
        private readonly SharedQueueHandler _sharedQueueHandler;

        public DisableLegacyLogger(
            IFunctionExecutor functionExecutor,
            SharedQueueHandler sharedQueueHandler
            )
        {
            this._functionExecutor = functionExecutor;
            this._sharedQueueHandler = sharedQueueHandler;
        }

        public bool Init(IFunctionIndex functions, IListenerFactory functionsListenerFactory, out IFunctionExecutor hostCallExecutor, out IListener listener, out HostOutputMessage hostOutputMessage, string hostId, CancellationToken shutdownToken)
        {
            hostCallExecutor = new ShutdownFunctionExecutor(shutdownToken, _functionExecutor);

            IListener factoryListener = new ListenerFactoryListener(functionsListenerFactory, _sharedQueueHandler);
            IListener shutdownListener = new ShutdownListener(shutdownToken, factoryListener);
            listener = shutdownListener;
            hostOutputMessage = new DataOnlyHostOutputMessage();
            return false;
        }
    }
}
