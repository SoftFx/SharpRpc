using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    partial class TxBuffer
    {
        private class StreamProxy : System.IO.Stream
        {
            private readonly TxBuffer _buffer;

            public StreamProxy(TxBuffer buffer)
            {
                _buffer = buffer;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotImplementedException();
            public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
            public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotImplementedException();
            public override void SetLength(long value) => throw new NotImplementedException();

            public override void Write(byte[] buffer, int offset, int count)
            {
                _buffer.Write(buffer, offset, count);
            }
        }
    }
}
