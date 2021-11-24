// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpRpc
{
    internal class RxMessageReader : Stream, MessageReader
    {
        private IReadOnlyList<ArraySegment<byte>> _data;
        private int _currSegmentNo;
        private int _currOffset;

#if NET5_0_OR_GREATER
        private readonly BufferSequence<byte> _bsAdapter = new BufferSequence<byte>();
#endif

        public void Init(IReadOnlyList<ArraySegment<byte>> segments)
        {
#if NET5_0_OR_GREATER
            _bsAdapter.AddRange(segments);
#endif
            _data = segments;
            _currSegmentNo = 0;
            _currOffset = 0;
        }

        //public int MsgSize => _bsAdapter.Count;

        public void Clear()
        {
#if NET5_0_OR_GREATER
            _bsAdapter.Clear();
#endif
        }

#if NET5_0_OR_GREATER
        public System.Buffers.ReadOnlySequence<byte> ByteBuffer => _bsAdapter.GetSequence();
#endif

        public Stream ByteStream => this;

        #region Stream implementation

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var copied = 0;

            while (count > 0)
            {
                if (_currSegmentNo >= _data.Count)
                    return copied;

                var currSegment = _data[_currSegmentNo];
                var dataLeftInSegment = currSegment.Count - _currOffset;
                var toCopy = Math.Min(count, dataLeftInSegment);

#if NET5_0_OR_GREATER
                Buffer.BlockCopy(currSegment.Array, currSegment.Offset + _currOffset, buffer, offset, toCopy);
#else
                Array.Copy(currSegment.Array, currSegment.Offset + _currOffset, buffer, offset, toCopy);
#endif

                copied += toCopy;
                offset += toCopy;
                count -= toCopy;

                if (toCopy == dataLeftInSegment)
                {
                    _currSegmentNo++;
                    _currOffset = 0;
                }
                else
                    _currOffset += toCopy;

            }

            return copied;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

#endregion
    }
}
