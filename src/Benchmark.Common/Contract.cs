using SharpRpc;
using System;
using System.Collections.Generic;
using System.Text;

namespace Benchmark.Common
{
    [RpcContract]
    [RpcSerializer(SerializerChoice.DataContract)]
    [RpcSerializer(SerializerChoice.MessagePack)]
    [RpcSerializer(SerializerChoice.ProtobufNet)]
    public interface Contract
    {
        [Rpc(RpcType.ClientMessage)]
        void SendUpdate(FooEntity entity, int index);

        [Rpc(RpcType.ClientMessage)]
        void SendUpdate2(FooEntity entity);
    }
}
