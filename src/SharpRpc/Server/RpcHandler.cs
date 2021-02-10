using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    internal class RpcHandler
    {
        private Channel _dispatcher;

        public RpcHandler(ByteTransport channel, Endpoint endpoint)
        {
            _dispatcher = new Channel(channel, endpoint);
        }

        public Guid Id { get; } = Guid.NewGuid();
    }
}
