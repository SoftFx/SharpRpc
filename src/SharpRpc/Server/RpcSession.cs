using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    internal class RpcSession
    {
        private readonly Channel _msgChannel;
        private readonly RpcServiceBase _serviceImpl;

        public RpcSession(ByteTransport transport, ServiceBinding binding, Endpoint endpoint)
        {
            _serviceImpl = binding.CreateServiceImpl();

            _msgChannel = new Channel(transport, endpoint, binding.Serializer, _serviceImpl);
            
        }

        public Guid Id { get; } = Guid.NewGuid();
    }
}
