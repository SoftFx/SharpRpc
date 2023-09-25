// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    partial class TxBuffer
    {
        private class MessageMarker
        {
            private readonly TxBuffer _buffer;
            //private MessageHeader _currentHeader;
            private int _headerPos;
            private HeaderWriter _writer = new HeaderWriter();
            private bool _isChunkOpened;
            private bool _isSeMessage;

            public MessageMarker(TxBuffer buffer)
            {
                _buffer = buffer;
            }

            //public bool IsWritingMessage { get; private set; }

            public void OnMessageStart(bool isSeMessage)
            {
                _isSeMessage = isSeMessage;
                //_currentHeader = header;
                //IsWritingMessage = true;
            }

            public void OnMessageEnd()
            {
                CloseChunk(true);
                //IsWritingMessage = false;
            }

            public void OnAlloc()
            {
                if (!_isChunkOpened)
                    OpenChunk();
            }

            public void OnSegmentClose()
            {
                if (_isChunkOpened)
                    CloseChunk(false);
            }

            private void OpenChunk()
            {
                ReserveSpaceForHeader();
                _isChunkOpened = true;
            }

            private void CloseChunk(bool EoM)
            {
                WriteHeader(EoM);
                _isChunkOpened = false;
            }

            private void ReserveSpaceForHeader()
            {
                _headerPos = _buffer.CurrentOffset;
                _buffer.MoveOffset(MessageHeader.HeaderSize);
            }

            private void WriteHeader(bool isEoM)
            {
                var segment = _buffer.CurrentSegment;
                var chunkSize = (ushort)(_buffer.CurrentOffset - _headerPos);

                var flags = MessageFlags.None;

                if (isEoM)
                    flags |= MessageFlags.EndOfMessage;

                if (_isSeMessage)
                    flags |= MessageFlags.SeMessage;

                _writer.WriteChunkHeader(segment, _headerPos, chunkSize, flags);
            }

            //private int CalcChunkCapacity()
            //{
            //    return ushort.MaxValue - (_buffer.CurrentOffset - _headerPos);
            //}
        }
    }
}
