﻿// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using SharpRpc.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpRpc
{
    internal class RxMessageReader : Stream, MessageReader, ISegmetedBufferEnumerator
    {
        private IReadOnlyList<ArraySegment<byte>> _data;
        private ArraySegment<byte> _currSegmet;
        private int _currSegmentNo;
        private int _currIndex;

        public RxMessageReader()
        {
            Se = new SimplifiedDecoder(this);
        }

#if NET5_0_OR_GREATER
        private readonly BufferSequence<byte> _bsAdapter = new BufferSequence<byte>();
#endif

        public void Init(IReadOnlyList<ArraySegment<byte>> segments, long messageSize)
        {
#if NET5_0_OR_GREATER
            _bsAdapter.AddRange(segments);
#endif
            MessageSize = messageSize;
            _data = segments;
            _currSegmentNo = 0;
            _currIndex = 0;
            _currSegmet = segments[0];
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

        public long MessageSize { get; private set; }
        //public IReadOnlyList<ArraySegment<byte>> RawData => _data;
        public SimplifiedDecoder Se { get; }
        public Stream ByteStream => this;

        private void GetNextSegmentIfRequired()
        {
            if (_currIndex >= _currSegmet.Count)
                GetNextSegment();
        }

        private void GetNextSegment()
        {
            _currSegmentNo++;
            _currIndex = 0;

            if (_currSegmentNo < _data.Count)
                _currSegmet = _data[_currSegmentNo];
            else
                _currSegmet = default;
        }

        private void AdvancePosition(int byValue)
        {
            _currIndex += byValue;
            GetNextSegmentIfRequired();
        }

        #region ISegmetedBufferEnumerator

        byte[] ISegmetedBufferEnumerator.Page => _currSegmet.Array;
        int ISegmetedBufferEnumerator.PageSize => _currSegmet.Count;
        int ISegmetedBufferEnumerator.PageOffset => _currSegmet.Offset;
        int ISegmetedBufferEnumerator.PageIndex => _currIndex;
        void ISegmetedBufferEnumerator.Advance(int value) => AdvancePosition(value);

        #endregion

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
                if (_currSegmet.Array == null)
                    return copied;

                var dataLeftInSegment = _currSegmet.Count - _currIndex;
                var copySize = Math.Min(count, dataLeftInSegment);

                Array.Copy(_currSegmet.Array, _currSegmet.Offset + _currIndex, buffer, offset, copySize);

                copied += copySize;
                offset += copySize;
                count -= copySize;

                AdvancePosition(copySize);
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
