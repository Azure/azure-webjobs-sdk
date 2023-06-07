// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Rpc
{
    /// <summary>
    /// Log messages.
    /// </summary>
    internal static partial class Logs
    {
        [LoggerMessage(EventId = 1400, Level = LogLevel.Trace, Message = "Begin applying RPC host/worker extensions.")]
        public static partial void ApplyRpcExtensionsBegin(this ILogger logger);

        [LoggerMessage(EventId = 1401, Level = LogLevel.Trace, Message = "Done applying RPC host/worker extensions. {count} endpoints added.")]
        public static partial void ApplyRpcExtensionsEnd(this ILogger logger, int count);

        [LoggerMessage(EventId = 1402, Level = LogLevel.Error, Message = "Error applying RPC host/worker extensions.")]
        public static partial void ApplyRpcExtensionsError(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = 1403, Level = LogLevel.Debug, Message = "gRPC extension added for host/worker communication. Extension: {extension}, service: {service}.")]
        public static partial void GrpcServiceApplied(this ILogger logger, string extension, string service);
    }
}
