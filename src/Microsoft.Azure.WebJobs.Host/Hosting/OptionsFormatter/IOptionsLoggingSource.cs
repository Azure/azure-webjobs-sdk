// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks.Dataflow;

namespace Microsoft.Azure.WebJobs.Hosting
{
    /// <summary>
    /// A source for buffering options log messages that can be consumed by <see cref="OptionsLoggingService"/>.
    /// </summary>
    public interface IOptionsLoggingSource
    {
        /// <summary>
        /// Gets the stream of buffered log messages.
        /// </summary>
        ISourceBlock<string> LogStream { get; }

        /// <summary>
        /// Buffers an options log message for later consumption.
        /// </summary>
        /// <param name="optionLog">The formatted options log message.</param>
        void LogOptions(string optionLog);
    }
}
