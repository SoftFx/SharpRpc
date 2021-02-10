using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpRpc
{
    public interface IMessage
    {
        //void Serialize(MessageWriter writer);
    }

    //internal class CommonMessage<T> : IMessage
    //{

    //}

    //internal class Message<T> : IMessage
    //{
    //}

    //public abstract class PreserializedMessage<T> : IMessage
    //{
    //}

    public interface MessageWriter
    {
        IBufferWriter<byte> ByteBuffer { get; } 
        Stream ByteStream { get; }
    }

    public interface MessageReader
    {
        ReadOnlySequence<byte> ByteBuffer { get; }
        Stream ByteStream { get; }
    }
}
