using System;
using System.Collections.Generic;

namespace SharpRpc
{
    [Flags]
    public enum MessageFlags : byte
    {
        None                = 0,
        EndOfMessage        = 1,
        TotalMask           = 0b1
    }

    //public enum MessageType
    //{
    //    System      = 1,
    //    User        = 2,
    //    Stream      = 3
    //}

    internal struct MessageHeader
    {
        //public MessageType MsgType { get; set; }
        public uint Size { get; set; }
        //public ushort Recipient { get; set; }

        public const int HeaderSize = 1 + 2;

        //public static int GetSize(MessageFlags type)
        //{
        //    return 1 + 2;
        //}

        //public static int GetSizeForChunk()
        //{
        //    return 1 + 2;
        //}
    }

    internal class HeaderWriter
    {
        private readonly BitTools _bitConverter = BitTools.Create();

        public int GetMessageHeaderSize(MessageHeader currentHeader)
        {
            return MessageHeader.HeaderSize;
        }

        public void WriteChunkHeader(byte[] buffer, int offset, ushort chunkSize, bool isEndOfMessage)
        {
            var flags = MessageFlags.None;

            if (isEndOfMessage)
                flags |= MessageFlags.EndOfMessage;

            // write flags
            buffer[offset++] = (byte)flags;

            // write size
            _bitConverter.Write(chunkSize, buffer, ref offset);
        }
    }

    internal class HeaderParser
    {
        private readonly BitTools _bitConverter = BitTools.Create();
        private readonly byte[] _bytes = new byte[MessageHeader.HeaderSize];
        private int _bytesCount = 0;
        private int _bytesToRead = 0;

        //public MessageHeader Header { get; private set; }

        public bool IsChunk { get; private set; }
        public ushort ChunkSize { get; private set; }
        public bool IsEoM { get; private set; }
        public int StreamId { get; private set; }

        public ParserRetCode ParseNextByte(byte b)
        {
            if (_bytesToRead == 0)
            {
                // flags
                var flags = (MessageFlags)b;

                if ((flags | MessageFlags.TotalMask) != MessageFlags.TotalMask)
                    return ParserRetCode.Error;

                _bytesToRead = MessageHeader.HeaderSize;
            }

            _bytes[_bytesCount++] = b;

            if (_bytesToRead == _bytesCount)
            {
                InterpreteBytes();
                _bytesCount = 0;
                _bytesToRead = 0;
                return ParserRetCode.Complete;
            }

            return ParserRetCode.Incomplete;
        }

        private void InterpreteBytes()
        {
            var offset = 0;

            // read flags
            var flags = (MessageFlags)_bytes[offset++];

            IsEoM = (flags & MessageFlags.EndOfMessage) > 0;
            IsChunk = !IsEoM;

            // read size
            ChunkSize = _bitConverter.ReadUshort(_bytes, ref offset);
            ChunkSize -= (ushort)_bytesToRead;
        }
    }

    internal enum ParserRetCode
    {
        Incomplete,
        Complete,
        Error
    }
}
