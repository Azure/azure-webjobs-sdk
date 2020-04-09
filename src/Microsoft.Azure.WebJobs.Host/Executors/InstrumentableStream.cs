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
        private static readonly bool _cacheEnabled = false;
        private readonly ILogger _logger;
        private readonly Stream _inner;
        private readonly InstrumentableObjectMetadata _metadata;
        private readonly Stopwatch _timeRead;
        private readonly Stopwatch _timeWrite;
        private readonly CacheClient _cacheClient;
        private long _countRead;
        private bool _logged;
        // TODO check if counting of written bytes is correct (count variable is *max* bytes to write, not those actually written)
        private long _countWrite;
        // TODO TEMP
        // TODO right now, if something is found from the cache, we assume (wrongly) that it will not be modified
        // We need to work on invalidating cache and writing dirty data back to storage
        private readonly bool _foundInCache;

        public InstrumentableStream(InstrumentableObjectMetadata metadata, Stream inner, ILogger logger)
        {
            _inner = inner;
            _logger = logger;
            _metadata = metadata;
            _timeRead = new Stopwatch();
            _timeWrite = new Stopwatch();
            _countRead = 0;
            _countWrite = 0;
            _foundInCache = false;
            _logged = false;
            if (metadata.TryGetValue("Uri", out string name) && _cacheEnabled)
            {
                try
                {
                    long length = _inner.Length;
                    _cacheClient = new CacheClient(name, length);
                }
                // TODO catch NotSupportedException
                catch 
                {
                    _cacheClient = new CacheClient(name);
                }
                _foundInCache = _cacheClient.ContainsKey();
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
                if (_foundInCache)
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
                _cacheClient.FlushToCache();
            }
            LogStatus();
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (_cacheEnabled && _cacheClient.ContainsKey())
            {
                _cacheClient.CacheHit = true;
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
                    _cacheClient.StartWriteTask(_inner.Position, buffer, offset, count);
                }
                _timeWrite.Start();
                _inner.Write(buffer, offset, count);
                _countWrite += count;
            }
            finally
            {
                _timeWrite.Stop();
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                if (_cacheEnabled)
                {
                    _cacheClient.StartWriteTask(_inner.Position, buffer, offset, count);
                }
                Task baseTask = _inner.WriteAsync(buffer, offset, count, cancellationToken);
                return WriteAsyncCore(baseTask);
            }
            finally
            {
                _countWrite += count;
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
                _cacheClient.WriteByte(value);
            }
            _inner.WriteByte(value);
            _countWrite++;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                if (_cacheEnabled && _cacheClient.ContainsKey())
                {
                    _cacheClient.CacheHit = true;
                    _timeRead.Start();
                    var bytesRead = _cacheClient.ReadAsync(buffer, offset, count).Result;
                    _countRead += bytesRead;
                    return bytesRead;
                }
                else
                {
                    _timeRead.Start();
                    long startPosition = _inner.Position;
                    var bytesRead = _inner.Read(buffer, offset, count);
                    if (_cacheEnabled)
                    {
                        _cacheClient.StartWriteTask(startPosition, buffer, offset, bytesRead);
                    }
                    _countRead += bytesRead;
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
            if (_cacheEnabled && _cacheClient.ContainsKey())
            {
                _cacheClient.CacheHit = true;
                return _cacheClient.ReadAsync(buffer, offset, count, cancellationToken);
            }
            else
            {
                long startPosition = _inner.Position;
                try
                {
                    return _inner.ReadAsync(buffer, offset, count, cancellationToken);
                }
                finally
                {
                    if (_cacheEnabled)
                    {
                        _cacheClient.StartWriteTask(startPosition, buffer, offset, count);
                    }
                }
            }
        }

        public override int ReadByte()
        {
            int read;

            if (_cacheEnabled && _cacheClient.ContainsKey())
            {
                _cacheClient.CacheHit = true;
                read = _cacheClient.ReadByte();
            }
            else
            {
                read = base.ReadByte();
                if (_cacheEnabled)
                {
                    _cacheClient.WriteByte(Convert.ToByte(read));
                }
            }

            if (read != -1)
            {
                _countRead++;
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
            stringBuilder.Append(", ReadBytes: ");
            stringBuilder.Append(_countRead);
            stringBuilder.Append(", WriteBytes: ");
            stringBuilder.Append(_countWrite);
            stringBuilder.Append(", CacheHit: ");
            stringBuilder.Append(_cacheEnabled ? _cacheClient?.CacheHit : false);
            stringBuilder.Append(", Metadata: {");
            stringBuilder.Append(_metadata);
            stringBuilder.Append("}");
            
            String logString = stringBuilder.ToString();

            // TODO see what ? does and if we can get rid of null check
            if (_logger != null)
            {
                _logger?.Log(LogLevel.Information, 0, logString, null, (s, e) => s);
            }

            _logged = true;
        }
    }
}
