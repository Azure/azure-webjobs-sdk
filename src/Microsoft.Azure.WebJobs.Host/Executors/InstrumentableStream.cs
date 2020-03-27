﻿// Copyright (c) .NET Foundation. All rights reserved.
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
        private readonly ILogger _logger;
        private readonly Stream _inner;
        private readonly InstrumentableObjectMetadata _metadata;
        private readonly Stopwatch _timeRead;
        private readonly Stopwatch _timeWrite;
        private long _countRead;
        // TODO check if counting of written bytes is correct (count variable is *max* bytes to write, not those actually written)
        private long _countWrite;

        public InstrumentableStream(InstrumentableObjectMetadata metadata, Stream inner, ILogger logger)
        {
            _inner = inner;
            _logger = logger;
            _metadata = metadata;
            _timeRead = new Stopwatch();
            _timeWrite = new Stopwatch();
            _countRead = 0;
            _countWrite = 0;
        }

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
            get { return _inner.Position; }
            set { _inner.Position = value; }
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
            LogStatus();
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return _inner.CopyToAsync(destination, bufferSize, cancellationToken);
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
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
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
            _inner.WriteByte(value);
            _countWrite++;
        }
        

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                _timeRead.Start();
                var bytesRead = _inner.Read(buffer, offset, count);
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
            Task<int> baseTask = _inner.ReadAsync(buffer, offset, count, cancellationToken);
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

        private void LogStatus()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("ReadTime: ");
            stringBuilder.Append(_timeRead.ElapsedMilliseconds);
            stringBuilder.Append(", WriteTime: ");
            stringBuilder.Append(_timeWrite.ElapsedMilliseconds);
            stringBuilder.Append(", ReadBytes: ");
            stringBuilder.Append(_countRead);
            stringBuilder.Append(", WriteBytes: ");
            stringBuilder.Append(_countWrite);
            stringBuilder.Append(", Metadata: {");
            stringBuilder.Append(_metadata);
            stringBuilder.Append("}");
            
            String logString = stringBuilder.ToString();

            // TODO see what ? does and if we can get rid of null check
            if (_logger != null)
            {
                _logger?.Log(LogLevel.Information, 0, logString, null, (s, e) => s);
            }
        }
    }
}