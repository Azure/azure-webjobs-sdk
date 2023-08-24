// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Timers;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class HeartbeatFunctionExecutor : DelegatingFunctionExecutor
    {
        private readonly IRecurrentCommand _heartbeatCommand;
        private readonly IWebJobsExceptionHandler _exceptionHandler;

        public HeartbeatFunctionExecutor(IRecurrentCommand heartbeatCommand,
            IWebJobsExceptionHandler exceptionHandler, IFunctionExecutor innerExecutor) : base(innerExecutor)
        {
            _heartbeatCommand = heartbeatCommand;
            _exceptionHandler = exceptionHandler;
        }

        public override async Task<IDelayedException> TryExecuteAsync(IFunctionInstance instance, CancellationToken cancellationToken)
        {
            IDelayedException result;

            using (ITaskSeriesTimer timer = CreateHeartbeatTimer(_exceptionHandler))
            {
                await _heartbeatCommand.TryExecuteAsync(cancellationToken);
                timer.Start();

                result = await base.TryExecuteAsync(instance, cancellationToken);

                await timer.StopAsync(cancellationToken);
            }

            return result;
        }

        private ITaskSeriesTimer CreateHeartbeatTimer(IWebJobsExceptionHandler exceptionHandler)
        {
            return LinearSpeedupStrategy.CreateTimer(_heartbeatCommand, HeartbeatIntervals.NormalSignalInterval,
                HeartbeatIntervals.MinimumSignalInterval, exceptionHandler);
        }
    }
}
