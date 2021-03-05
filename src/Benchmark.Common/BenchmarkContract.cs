using SharpRpc;
using System;
using System.Collections.Generic;
using System.Text;

namespace Benchmark.Common
{
    [RpcContract]
    [RpcSerializer(SerializerChoice.MessagePack)]
    public interface BenchmarkContract
    {
        [Rpc(RpcType.ClientMessage)]
        void SendUpdate(FooEntity entity);
    }
}
