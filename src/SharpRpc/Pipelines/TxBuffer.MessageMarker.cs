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

            public MessageMarker(TxBuffer buffer)
            {
                _buffer = buffer;
            }

            //public bool IsWritingMessage { get; private set; }

            public void OnMessageStart()
            {
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

                _writer.WriteChunkHeader(segment, _headerPos, chunkSize, isEoM);
            }

            //private int CalcChunkCapacity()
            //{
            //    return ushort.MaxValue - (_buffer.CurrentOffset - _headerPos);
            //}
        }
    }
}
