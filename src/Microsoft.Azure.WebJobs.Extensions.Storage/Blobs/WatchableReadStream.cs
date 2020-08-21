// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Blobs
{
    internal class WatchableReadStream : DelegatingStream, IWatcher
    {
        private readonly ILogger _logger;
        private readonly ICloudBlob _blob;
        private readonly Stopwatch _timeRead = new Stopwatch();
        private readonly long _totalLength;

        private long _countRead;
        private bool _logged;

        public WatchableReadStream(Stream inner)
            : base(inner)
        {
            _totalLength = inner.Length;
            _logged = false;
        }

        public WatchableReadStream(Stream inner, ICloudBlob blob, ILogger logger)
            : base(inner)
        {
            _totalLength = inner.Length;
            _logged = false;
            _logger = logger;
            _blob = blob;
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            Task baseTask = base.CopyToAsync(destination, bufferSize, cancellationToken);
            return CopyToAsyncCore(baseTask);
        }

        private async Task CopyToAsyncCore(Task baseTask)
        {
            try
            {
                _timeRead.Start();
                await baseTask;
                _countRead += _totalLength;
            }
            finally
            {
                _timeRead.Stop();
            }
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            Task<int> baseTask = Task<int>.Factory.FromAsync(base.BeginRead, base.EndRead, buffer, offset, count, state: null);
            return new TaskAsyncResult<int>(ReadAsyncCore(baseTask), callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            TaskAsyncResult<int> taskResult = (TaskAsyncResult<int>)asyncResult;
            return taskResult.End();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                _timeRead.Start();
                var bytesRead = base.Read(buffer, offset, count);
                _countRead += bytesRead;
                return bytesRead;
            }
            finally
            {
                _timeRead.Stop();
            }
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Task<int> baseTask = base.ReadAsync(buffer, offset, count, cancellationToken);
            return ReadAsyncCore(baseTask);
        }

        private async Task<int> ReadAsyncCore(Task<int> baseTask)
        {
            try
            {
                _timeRead.Start();
                int bytesRead = await baseTask;
                _countRead += bytesRead;
                return bytesRead;
            }
            finally
            {
                _timeRead.Stop();
            }
        }

        public override int ReadByte()
        {
            int read = base.ReadByte();

            if (read != -1)
            {
                _countRead++;
            }

            return read;
        }

        public override void Close()
        {
            base.Close();
            Log();
        }

        private void Log()
        {
            if (_logged)
            {
                return;
            }

            if (_blob == null || _logger == null)
            {
                return;
            }

            try
            {
                List<string> statisticsList = new List<string>
                {
                    $"BlobName: {_blob.Name}",
                    $"ContainerName: {_blob.Container.Name}",
                    $"Length: {_blob.Properties.Length}",
                    $"ETag: {_blob.Properties.ETag}",
                    $"BlobType: {_blob.BlobType}",
                    $"BytesRead: {_countRead}",
                    $"ElapsedTime: {_timeRead.Elapsed}",
                };

                string statistics = string.Join(", ", statisticsList);
                string logMessage = $"ReadStream ({statistics})";
                _logger.LogInformation(logMessage);
                _logged = true;
            }
            catch
            {
                return;
            }
        }

        public ParameterLog GetStatus()
        {
            return new ReadBlobParameterLog
            {
                BytesRead = _countRead,
                Length = _totalLength,
                ElapsedTime = _timeRead.Elapsed
            };
        }
    }
}
