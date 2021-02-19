using SharpRpc;
using System;
using System.Collections.Generic;
using System.Text;

namespace Benchmark.Common
{
    [RpcContract]
    public interface Contract
    {
        [Rpc(RpcType.ClientMessage)]
        void SendUpdate(FooEntity entity, int index);
    }
}
