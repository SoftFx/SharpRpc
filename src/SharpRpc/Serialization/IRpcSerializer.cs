using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public interface IRpcSerializer
    {
        void Serialize(IMessage message, MessageWriter writer);
        IMessage Deserialize(MessageReader reader);
    }

    //public interface IMessageSerializer<T>
    //{
    //    void Serialize(T entity, MessageWriter writer);
    //}

    //public interface IMessageDeserializer<T>
    //{
    //    void Deserialize(T entity, MessageReader reader);
    //}
}
