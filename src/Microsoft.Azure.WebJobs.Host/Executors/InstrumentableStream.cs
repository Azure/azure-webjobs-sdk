// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    public class InstrumentableStream : Stream
    {
        private readonly bool _cacheEnabled;
        private readonly ILogger _logger;
        private readonly Stream _inner;
        private readonly InstrumentableObjectMetadata _metadata;
        private readonly Stopwatch _timeRead;
        private readonly Stopwatch _timeWrite;
        private readonly CacheClient _cacheClient;
        private long _countReadFromCache;
        private long _countReadFromStorage;
        private long _countWrite;
        private bool _logged;

        public InstrumentableStream(InstrumentableObjectMetadata metadata, Stream inner, ILogger logger, bool cacheTrigger)
        {
            _inner = inner;
            _logger = logger;
            _metadata = metadata;
            _timeRead = new Stopwatch();
            _timeWrite = new Stopwatch();
            _countReadFromCache = 0;
            _countReadFromStorage = 0;
            _countWrite = 0;
            _logged = false;
            // TODO hack - if cachetrigger then we are not going to cache anything else for now - needs to be fixed for later
            _cacheEnabled = !cacheTrigger;
            if (metadata.TryGetValue("Uri", out string name) && metadata.TryGetValue("ETag", out string etag) && _cacheEnabled && !cacheTrigger)
            {
                _cacheClient = new CacheClient(name, etag);
            }
        }

        ~InstrumentableStream()
        {
            LogStatus();
        }

        // TODO need to also sync setting/getting of properties with cached stream instead of inner 
        public override bool CanRead
        {
            get { return _inner.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _inner.CanSeek; }
        }

        public override bool CanTimeout
        {
            get { return _inner.CanTimeout; }
        }

        public override bool CanWrite
        {
            get { return _inner.CanWrite; }
        }

        public override long Length
        {
            get { return _inner.Length; }
        }

        public override long Position
        {
            get
            {
                if (_cacheEnabled && _cacheClient.ReadFromCache)
                {
                    return _cacheClient.GetPosition();
                }
                else
                {
                    return _inner.Position;
                }
            }

            set
            {
                _inner.Position = value;
                if (_cacheEnabled)
                {
                    _cacheClient.SetPosition(value);
                }
            }
        }

        public override int ReadTimeout
        {
            get { return _inner.ReadTimeout; }
            set { _inner.ReadTimeout = value; }
        }

        public override int WriteTimeout
        {
            get { return _inner.WriteTimeout; }
            set { _inner.WriteTimeout = value; }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback,
            object state)
        {
            return _inner.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return _inner.EndRead(asyncResult);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback,
            object state)
        {
            return _inner.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            _inner.EndWrite(asyncResult);
        }

        public override void Close()
        {
            _inner.Close();
            if (_cacheEnabled)
            {
                _cacheClient.Close();
            }
            LogStatus();
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (_cacheEnabled && _cacheClient.ReadFromCache)
            {
                return _cacheClient.CopyToAsync(destination, bufferSize, cancellationToken);
            }
            else
            {
                return _inner.CopyToAsync(destination, bufferSize, cancellationToken);
            }
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _inner.FlushAsync(cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (_cacheEnabled)
            {
                _cacheClient.Seek(offset, origin);
            }
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
            if (_cacheEnabled)
            {
                _cacheClient.SetLength(value);
            }
        }

        public new void CopyTo(Stream destination)
        {
            _inner.CopyTo(destination);
        }

        public new void CopyTo(Stream destination, int bufferSize)
        {
            _inner.CopyTo(destination, bufferSize);
        }
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            try
            {
                if (_cacheEnabled)
                {
                    _cacheClient.StartWriteTask(_inner.Position, buffer, offset, count, -1);
                }
                _timeWrite.Start();
                long startPosition = _inner.Position;
                _inner.Write(buffer, offset, count);
                long written = _inner.Position - startPosition;
                _countWrite += written;
            }
            finally
            {
                _timeWrite.Stop();
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            long startPosition = _inner.Position;
            try
            {
                if (_cacheEnabled)
                {
                    _cacheClient.StartWriteTask(_inner.Position, buffer, offset, count, -1);
                }
                Task baseTask = _inner.WriteAsync(buffer, offset, count, cancellationToken);
                return WriteAsyncCore(baseTask);
            }
            finally
            {
                long written = _inner.Position - startPosition;
                _countWrite += written;
            }
        }

        private async Task WriteAsyncCore(Task baseTask)
        {
            try
            {
                _timeWrite.Start();
                await baseTask;
            }
            finally
            {
                _timeWrite.Stop();
            }
        }
        
        public override void WriteByte(byte value)
        {
            if (_cacheEnabled)
            {
                _cacheClient.WriteByte(_inner.Position, value);
            }
            _inner.WriteByte(value);
            _countWrite++;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                if (_cacheEnabled && _cacheClient.ReadFromCache && _cacheClient.ContainsBytes(_cacheClient.GetPosition(), _cacheClient.GetPosition() + count))
                {
                    _timeRead.Start();
                    int bytesRead = _cacheClient.ReadAsync(buffer, offset, count).Result;
                    _countReadFromCache += bytesRead;

                    // Whenever reading from the cache, make sure to keep the position of the remote stream also in sync
                    // This will ensure that if we get a cache miss at some point and start reading from the remote stream, we can resume reading from the same position
                    _inner.Position = _cacheClient.GetPosition();

                    return bytesRead;
                }
                else
                {
                    _timeRead.Start();
                    long startPosition = _inner.Position;
                    var bytesRead = _inner.Read(buffer, offset, count);
                    if (_cacheEnabled && bytesRead > 0)
                    {
                        _cacheClient.StartWriteTask(startPosition, buffer, offset, count, bytesRead);
                    }
                    _countReadFromStorage += bytesRead;
                    return bytesRead;
                }
            }
            finally
            {
                _timeRead.Stop();
            }
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Task<int> readTask = null;
            if (_cacheEnabled && _cacheClient.ReadFromCache && _cacheClient.ContainsBytes(_cacheClient.GetPosition(), _cacheClient.GetPosition() + count))
            {
                try
                {
                    readTask = _cacheClient.ReadAsync(buffer, offset, count, cancellationToken);
                    return readTask;
                }
                finally
                {
                    // Whenever reading from the cache, make sure to keep the position of the remote stream also in sync
                    // This will ensure that if we get a cache miss at some point and start reading from the remote stream, we can resume reading from the same position
                    _inner.Position = _cacheClient.GetPosition();
                    _countReadFromCache += readTask.Result;
                }
            }
            else
            {
                long startPosition = _inner.Position;
                try
                {
                    readTask = _inner.ReadAsync(buffer, offset, count, cancellationToken);
                    return readTask;
                }
                finally
                {
                    if (_cacheEnabled)
                    {
                        _cacheClient.StartWriteTask(startPosition, buffer, offset, count, readTask.Result);
                    }
                    _countReadFromStorage += readTask.Result;
                }
            }
        }

        public override int ReadByte()
        {
            int read;

            if (_cacheEnabled && _cacheClient.ReadFromCache && _cacheClient.ContainsByte(_cacheClient.GetPosition()))
            {
                read = _cacheClient.ReadByte();
                if (read != -1)
                {
                    _countReadFromCache++;
                }
            }
            else
            {
                long startPosition = _inner.Position;
                read = _inner.ReadByte();
                if (_cacheEnabled && read != -1)
                {
                    _cacheClient.WriteByte(startPosition, Convert.ToByte(read));
                }
                if (read != -1)
                {
                    _countReadFromStorage++;
                }
            }

            return read;
        }

        private void LogStatus()
        {
            if (_logged)
            {
                return;
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("ReadTime: ");
            stringBuilder.Append(_timeRead.ElapsedMilliseconds);
            stringBuilder.Append(", WriteTime: ");
            stringBuilder.Append(_timeWrite.ElapsedMilliseconds);
            stringBuilder.Append(", ReadBytesFromCache: ");
            stringBuilder.Append(_countReadFromCache);
            stringBuilder.Append(", ReadBytesFromStorage: ");
            stringBuilder.Append(_countReadFromStorage);
            stringBuilder.Append(", WriteBytes: ");
            stringBuilder.Append(_countWrite);
            stringBuilder.Append(", Metadata: {");
            stringBuilder.Append(_metadata);
            stringBuilder.Append("}");
            
            String logString = stringBuilder.ToString();

            if (_logger != null)
            {
                _logger.Log(LogLevel.Information, 0, logString, null, (s, e) => s);
            }

            _logged = true;
        }
    }
}
