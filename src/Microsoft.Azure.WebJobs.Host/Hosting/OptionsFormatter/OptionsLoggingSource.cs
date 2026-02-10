// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks.Dataflow;

namespace Microsoft.Azure.WebJobs.Hosting
{
    /// <summary>
    /// Default implementation of <see cref="IOptionsLoggingSource"/> that buffers log messages.
    /// </summary>
    public sealed class OptionsLoggingSource : IOptionsLoggingSource
    {
        private readonly BufferBlock<string> _buffer = new BufferBlock<string>();

        /// <inheritdoc />
        public ISourceBlock<string> LogStream => _buffer;

        /// <inheritdoc />
        public void LogOptions(string optionLog)
        {
            _buffer.Post(optionLog);
        }
    }
}
