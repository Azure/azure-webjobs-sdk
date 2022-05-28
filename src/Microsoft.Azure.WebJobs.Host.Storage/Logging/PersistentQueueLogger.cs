// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace WebJobs.Host.Storage.Logging
{
    internal class PersistentQueueLogger : IHostInstanceLogger, IFunctionInstanceLogger
    {
        private readonly IPersistentQueueWriter<PersistentQueueMessage> _queueWriter;

        public PersistentQueueLogger(IPersistentQueueWriter<PersistentQueueMessage> queueWriter)
        {
            if (queueWriter == null)
            {
                throw new ArgumentNullException("queueWriter");
            }

            _queueWriter = queueWriter;
        }

        public Task LogHostStartedAsync(HostStartedMessage message, CancellationToken cancellationToken)
        {
            return _queueWriter.EnqueueAsync(message, cancellationToken);
        }

        public string LogFunctionStarted(FunctionStartedMessage message)
        {
            return _queueWriter.EnqueueAsync(message, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public void LogFunctionCompleted(FunctionCompletedMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            _queueWriter.EnqueueAsync(message, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public void DeleteLogFunctionStarted(string startedMessageId)
        {
            if (String.IsNullOrEmpty(startedMessageId))
            {
                throw new ArgumentNullException("startedMessageId");
            }

            _queueWriter.DeleteAsync(startedMessageId, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
