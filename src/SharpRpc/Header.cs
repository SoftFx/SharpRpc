using System;
using System.Collections.Generic;

namespace SharpRpc
{
    [Flags]
    public enum MessageFlags : byte
    {
        MessageContinuation   = 0,
        SystemMessage         = 1,
        UserMessage           = 2,
        UserStreamMessage     = 3,
        EndOfMessage          = 4,
        //IsShortSize     = 16,
        //HasCrc          = 32,

        MessageTypeMask       = 0b11,
        TotalMask             = 0b111
    }

    public enum MessageType
    {
        System      = 1,
        User        = 2,
        Stream      = 3
    }

    internal struct MessageHeader
    {
        public MessageType MsgType { get; set; }
        //public uint Size { get; set; }
        public ushort Recipient { get; set; }

        public const int MaxSize = 1 + 2 + 2;

        public static int GetSize(MessageFlags type)
        {
            if (type == MessageFlags.UserStreamMessage)
                return 1 + 2 + 2;
            else
                return 1 + 2;
        }

        public static int GetSize(MessageType type)
        {
            if (type == MessageType.Stream)
                return 1 + 2 + 2;
            else
                return 1 + 2;
        }

        public static int GetSizeForChunk()
        {
            return 1 + 2;
        }
    }

    internal class HeaderWriter
    {
        private readonly BitTools _bitConverter = BitTools.Create();

        public int GetMessageHeaderSize(MessageHeader currentHeader)
        {
            return MessageHeader.GetSize(currentHeader.MsgType);
        }

        public int GetContinuationHeaderSize()
        {
            return MessageHeader.GetSizeForChunk();
        }

        public void WriteMessageHeader(MessageHeader header, byte[] buffer, int offset, ushort chunkSize, bool isEndOfMessage)
        {
            var flags = (MessageFlags)header.MsgType;

            if (isEndOfMessage)
                flags |= MessageFlags.EndOfMessage;

            // write flags
            buffer[offset++] = (byte)flags;

            // write size
            _bitConverter.Write(chunkSize, buffer, ref offset);

            if (header.MsgType == MessageType.Stream)
            {
                // write Recipient
                _bitConverter.Write(header.Recipient, buffer, ref offset);
            }
        }

        public void WriteContinuationHeader(MessageHeader header, byte[] buffer, int offset, ushort chunkSize, bool isEndOfMessage)
        {
            var flags = MessageFlags.MessageContinuation;

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
        private readonly byte[] _bytes = new byte[MessageHeader.MaxSize];
        private int _bytesCount = 0;
        private int _bytesToRead = 0;

        //public MessageHeader Header { get; private set; }

        public bool IsChunk { get; private set; }
        public MessageType Type { get; private set; }
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

                var msgType = flags & MessageFlags.MessageTypeMask;
                _bytesToRead = MessageHeader.GetSize(msgType);
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
            var msgType = flags & MessageFlags.MessageTypeMask;

            IsEoM = (flags & MessageFlags.EndOfMessage) > 0;

            if (msgType == MessageFlags.MessageContinuation)
                IsChunk = true;
            else
            {
                IsChunk = false;
                Type = (MessageType)msgType;
            }

            // read size
            ChunkSize = _bitConverter.ReadUshort(_bytes, ref offset);
            ChunkSize -= (ushort)_bytesToRead;

            if (msgType == MessageFlags.UserStreamMessage)
            {
                // read Recipient
                StreamId = _bitConverter.ReadInt(_bytes, ref offset);
            }
        }
    }

    internal enum ParserRetCode
    {
        Incomplete,
        Complete,
        Error
    }
}
