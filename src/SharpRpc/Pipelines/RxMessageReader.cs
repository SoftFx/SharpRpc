using SharpRpc.Lib;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpRpc
{
    internal class RxMessageReader : MessageReader
    {
        private readonly BufferSequence<byte> _bsAdapter = new BufferSequence<byte>();

        public void Init(IReadOnlyList<ArraySegment<byte>> segments)
        {
            _bsAdapter.AddRange(segments);
        }

        public int MsgSize => _bsAdapter.Count;

        public void Clear()
        {
            _bsAdapter.Clear();
        }

        public ReadOnlySequence<byte> ByteBuffer => _bsAdapter.GetSequence();

        public Stream ByteStream => throw new NotImplementedException();
    }
}
