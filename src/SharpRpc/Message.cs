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
        string CallId { get; set; }
        //int? FromRecipient { get; set; }
    }

    public interface IResponse : IMessage
    {
        string CallId { get; set; }
        //int? ToRecipient { get; }
    }

    public interface IResponse<T> : IMessage
    {
        T Result { get; }
    }

    public interface ISystemMessage : IMessage
    {
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
