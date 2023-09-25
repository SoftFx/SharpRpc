// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace SharpRpc
{
    public class PreserializeTool
    {
        private readonly IRpcSerializer[] _adapterById;
        private readonly SegmentWriter _writer = new SegmentWriter();
        private readonly IRpcSerializer _singleAdapter;

        public PreserializeTool(params IRpcSerializer[] adapters)
        {
            _adapterById = adapters;
            if (adapters.Length == 1)
                _singleAdapter = adapters[0];
        }

        public SegmentedByteArray SerializeOnSingleAdapter(IMessage message)
        {
            return PrebuildMessage(message, _singleAdapter);
        }

        public SegmentedByteArray[] SerializeOnAllAdapters(IMessage message)
        {
            var result = new SegmentedByteArray[_adapterById.Length];

            for (int i = 0; i < _adapterById.Length; i++)
                result[i] = PrebuildMessage(message, _adapterById[i]);

            return result;
        }

        private SegmentedByteArray PrebuildMessage(IMessage message, IRpcSerializer adapter)
        {
            adapter.Serialize(message, _writer);
            return _writer.CompleteWrite();
        }

#if NET5_0_OR_GREATER
        private class SegmentWriter : Stream, MessageWriter, System.Buffers.IBufferWriter<byte>
#else
        private class SegmentWriter : Stream, MessageWriter
#endif
        {
            private readonly int _memeoryMinSize = 128;
            private readonly int _segmentSize = 512;
            private SegmentedByteArray _data;
            private byte[] _currentSegment;
            private int _currentOffset;

            public SegmentWriter()
            {
                _data = new SegmentedByteArray();
                AllocateNewSegment();
            }

#if NET5_0_OR_GREATER
            public System.Buffers.IBufferWriter<byte> ByteBuffer => this;
#endif
            public Stream ByteStream => this;

            public SegmentedByteArray CompleteWrite()
            {
                if (_currentOffset > 0)
                    CompleteSegment();

                var result = _data;
                _data = new SegmentedByteArray();
                return result;
            }

            private void AllocateNewSegment()
            {
#if NET5_0_OR_GREATER
                _currentSegment = System.Buffers.ArrayPool<byte>.Shared.Rent(_segmentSize);
#else
                _currentSegment = new byte[_segmentSize];
#endif
            }

            private void CompleteSegment()
            {
                _data.Add(new ArraySegment<byte>(_currentSegment, 0, _currentOffset));
                AllocateNewSegment();
                _currentOffset = 0;
            }

            private void EnsureSpace(int sizeHint)
            {
                if (sizeHint <= 0)
                    sizeHint = _memeoryMinSize;

                if (_currentSegment.Length - _currentOffset < sizeHint)
                    CompleteSegment();
            }

            #region IBufferWriter<byte>

            public void Advance(int count)
            {
                _currentOffset += count;
            }

#if NET5_0_OR_GREATER
            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                EnsureSpace(sizeHint);
                return new Memory<byte>(_currentSegment, _currentOffset, _currentSegment.Length - _currentOffset);
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                EnsureSpace(sizeHint);
                return new Span<byte>(_currentSegment, _currentOffset, _currentSegment.Length - _currentOffset);
            }
#endif
            #endregion

            public void AdvanceWriteBuffer(int count)
            {
                Advance(count);
            }

            public ArraySegment<byte> AllocateWriteBuffer(int sizeHint = 0)
            {
                EnsureSpace(sizeHint);
                return new ArraySegment<byte>(_currentSegment, _currentOffset, _currentSegment.Length - _currentOffset);
            }

            #region Stream

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
                while (count > 0)
                {
                    var spaceLeft = _currentSegment.Length - _currentOffset;

                    if (spaceLeft == 0)
                    {
                        AllocateNewSegment();
                        spaceLeft = _currentSegment.Length;
                    }

                    var copySize = Math.Min(spaceLeft, count);
                    Buffer.BlockCopy(buffer, offset, _currentSegment, _currentOffset, copySize);

                    _currentOffset += copySize;
                    count -= copySize;
                }
            }

            #endregion
        }
    }
}
