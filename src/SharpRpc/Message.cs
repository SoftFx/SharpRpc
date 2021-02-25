using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpRpc
{
    public interface IMessage
    {
    }

    public interface IRequest : IMessage
    {
    }

    public interface IResponse : IMessage
    {
    }

    public interface IResponse<T> : IMessage
    {
        T Result { get; }
    }

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
