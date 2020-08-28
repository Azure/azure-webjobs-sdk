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
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Blobs
{
    internal class WatchableReadStream : DelegatingStream, IWatcher
    {
        private readonly ILogger _logger;
        private readonly ICloudBlob _blob;
        private readonly JObject _blobAccessLog = new JObject();
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
            _logger = logger ?? throw new ArgumentNullException("logger");
            _blob = blob ?? throw new ArgumentNullException("blob");
            _blobAccessLog["Name"] = blob.Name;
            _blobAccessLog["ContainerName"] = blob.Container.Name;
            _blobAccessLog["Type"] = blob.BlobType.ToString();
            _blobAccessLog["Length"] = blob.Properties.Length;
            _blobAccessLog["ETag"] = blob.Properties.ETag;
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
            if (_logged || _logger == null || _blob == null)
            {
                return;
            }

            _logger.LogDebug($"ReadStream: {_blobAccessLog}");
            _blobAccessLog["ElapsedTimeOnRead"] = _timeRead.Elapsed;
            _blobAccessLog["BytesRead"] = _countRead;
            _logged = true;
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
