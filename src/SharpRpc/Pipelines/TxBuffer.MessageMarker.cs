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
            private MessageHeader _currentHeader;
            private bool _firstHeader;
            private int _headerPos;
            private HeaderWriter _writer = new HeaderWriter();
            private bool _isChunkOpened;

            public MessageMarker(TxBuffer buffer)
            {
                _buffer = buffer;
            }

            //public bool IsWritingMessage { get; private set; }

            public void OnMessageStart(MessageHeader header)
            {
                _currentHeader = header;
                _firstHeader = true;
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
                var headerSize = _firstHeader ? _writer.GetMessageHeaderSize(_currentHeader)
                    : _writer.GetContinuationHeaderSize();

                _headerPos = _buffer.CurrentOffset;
                _buffer.MoveOffset(headerSize);
            }

            private void WriteHeader(bool isEoM)
            {
                var segment = _buffer.CurrentSegment;
                var chunkSize = (ushort)(_buffer.CurrentOffset - _headerPos);

                if (_firstHeader)
                    _writer.WriteMessageHeader(_currentHeader, segment, _headerPos, chunkSize, isEoM);
                else
                    _writer.WriteContinuationHeader(_currentHeader, segment, _headerPos, chunkSize, isEoM);

                _firstHeader = false;
            }

            private int CalcChunkCapacity()
            {
                return ushort.MaxValue - (_buffer.CurrentOffset - _headerPos);
            }
        }
    }
}
