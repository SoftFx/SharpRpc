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

        [Rpc(RpcType.ClientCall)]
        void SendUpdate2(FooEntity entity);
    }

    public static class BenchmarkContractCfg
    {
        public static void ConfigureEndpoint(Endpoint endpoint)
        {
            endpoint.RxTimeout = TimeSpan.FromSeconds(5);
            endpoint.EnableKeepAlive(TimeSpan.FromSeconds(1));
        }

        public static int GetPort(bool secure)
        {
            return secure ? 8413 : 8412;
        }
    }
}
